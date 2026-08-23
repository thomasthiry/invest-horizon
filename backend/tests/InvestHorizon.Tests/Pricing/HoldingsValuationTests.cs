using FluentAssertions;
using InvestHorizon.Application.CostEngine;
using InvestHorizon.Application.Interfaces;
using InvestHorizon.Application.Services;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;
using Xunit;

namespace InvestHorizon.Tests.Pricing;

public class HoldingsValuationTests
{
    private static readonly Guid PortfolioId = Guid.NewGuid();
    private static readonly Guid InstrumentId = Guid.NewGuid();

    // totalCostEur is the whole cost basis; buyBrokerFee/buyTob are the part of it that was cost
    // rather than shares, mirroring the engine's TotalCost = AmountEur + BrokerFee + TobAmount.
    private static Transaction Buy(
        decimal qty,
        decimal totalCostEur,
        string currency,
        Broker broker = Broker.Keytrade,
        InstrumentType type = InstrumentType.Share,
        decimal buyBrokerFee = 0m,
        decimal buyTob = 0m) =>
        new()
        {
            Id = Guid.NewGuid(),
            PortfolioId = PortfolioId,
            InstrumentId = InstrumentId,
            Instrument = new Instrument { Id = InstrumentId, Isin = "US0378331005", Name = "Apple", Currency = currency, Type = type },
            Side = TransactionSide.Buy,
            Broker = broker,
            Currency = currency,
            Quantity = qty,
            RemainingQuantity = qty,
            AmountEur = totalCostEur - buyBrokerFee - buyTob,
            BrokerFee = buyBrokerFee,
            TobAmount = buyTob,
            TotalCost = totalCostEur,
        };

    private static HoldingsService Service(
        ITransactionRepository txs,
        IInstrumentPriceRepository prices,
        IFxRateProvider fx) =>
        new(txs, new IInstrumentRepositoryFake(), prices, fx,
            new ExitCostEstimator(
                [new KeytradeFeeCalculator(), new RevolutFeeCalculator(), new MeDirectFeeCalculator()],
                new BelgianTobCalculator()));

    private static IInstrumentPriceRepositoryFake PriceOf(
        decimal priceNative, string currency, DateTime? asOf = null, DateTime? fetchedAt = null) =>
        new(new InstrumentPrice
        {
            InstrumentId = InstrumentId,
            PriceNative = priceNative,
            Currency = currency,
            AsOf = asOf ?? default,
            FetchedAt = fetchedAt ?? default,
            Source = "Yahoo",
        });

    [Fact]
    public async Task GetHoldings_ComputesMarketValueAndUnrealizedInEur()
    {
        // 10 shares bought for EUR 1000 total; now $200 each, 1 EUR = 1.25 USD -> EUR 1,600 market value.
        var txs = new ITransactionRepositoryFake([Buy(10m, 1000m, "USD")]);
        var asOf = new DateTime(2026, 6, 12, 17, 0, 0, DateTimeKind.Utc);
        var fetchedAt = new DateTime(2026, 6, 13, 9, 42, 0, DateTimeKind.Utc);
        var svc = Service(txs, PriceOf(200m, "USD", asOf, fetchedAt), new FxFake(1.25m));

        var holdings = await svc.GetHoldingsAsync(PortfolioId);

        holdings.Should().HaveCount(1);
        var h = holdings[0];
        h.MarketValueEur.Should().BeApproximately(1600m, 0.001m);
        // Exit costs: Keytrade 5.95 (1,600 is in the <= 2,500 tier) + TOB 0.35% of 1,600 = 5.60.
        h.EstimatedSellCostsEur.Should().BeApproximately(11.55m, 0.001m);
        h.UnrealizedGainEur.Should().BeApproximately(1600m - 11.55m - 1000m, 0.001m);
        h.CurrentPriceNative.Should().Be(200m);
        h.PriceCurrency.Should().Be("USD");
        h.PriceAsOf.Should().Be(asOf);
        h.PriceFetchedAt.Should().Be(fetchedAt);
    }

    [Fact]
    public async Task GetHoldings_ChargesOneSellOrderPerBroker_ForLotsAtTheSameBroker()
    {
        // 100 shares @ EUR 40 = EUR 4,000, all at Keytrade across three lots -> ONE sell order.
        // Keytrade 14.95 (<= 10,000 block) + TOB 0.35% of 4,000 = 14.00.
        var txs = new ITransactionRepositoryFake([
            Buy(30m, 1_200m, "EUR"),
            Buy(50m, 2_000m, "EUR"),
            Buy(20m, 800m, "EUR"),
        ]);
        var svc = Service(txs, PriceOf(40m, "EUR"), new FxFake(1m));

        var h = (await svc.GetHoldingsAsync(PortfolioId))[0];

        h.MarketValueEur.Should().Be(4_000m);
        h.EstimatedSellCostsEur.Should().Be(14.95m + 14.00m);
        h.UnrealizedGainEur.Should().Be(4_000m - 28.95m - 4_000m);
    }

    [Fact]
    public async Task GetHoldings_SumsExitCostsOfBothBrokers_WhenAPositionIsSplit()
    {
        // Same 100 shares @ EUR 40, but 60 at Keytrade (2,400) and 40 at Revolut (1,600).
        //   Keytrade 5.95 + TOB 8.40   ·   Revolut 4.00 + TOB 5.60
        var txs = new ITransactionRepositoryFake([
            Buy(60m, 2_400m, "EUR", Broker.Keytrade),
            Buy(40m, 1_600m, "EUR", Broker.Revolut),
        ]);
        var svc = Service(txs, PriceOf(40m, "EUR"), new FxFake(1m));

        var h = (await svc.GetHoldingsAsync(PortfolioId))[0];

        h.MarketValueEur.Should().Be(4_000m);
        h.EstimatedSellCostsEur.Should().Be(5.95m + 8.40m + 4.00m + 5.60m);
        h.UnrealizedGainEur.Should().Be(4_000m - 23.95m - 4_000m);

        // The breakdown the tooltip renders: one line per broker, reconciling to the total.
        h.ExitCostOrders.Should().HaveCount(2);
        h.ExitCostOrders!.Sum(o => o.OrderValueEur).Should().Be(4_000m);
        h.ExitCostOrders!.Sum(o => o.TotalEur).Should().Be(h.EstimatedSellCostsEur);
        var keytrade = h.ExitCostOrders!.Single(o => o.Broker == Broker.Keytrade);
        keytrade.Quantity.Should().Be(60m);
        keytrade.UnitPriceEur.Should().Be(40m);
        keytrade.OrderValueEur.Should().Be(2_400m);
        keytrade.BrokerFeeEur.Should().Be(5.95m);
        keytrade.TobEur.Should().Be(8.40m);
    }

    [Fact]
    public async Task GetHoldings_SplitsTheCostBasisIntoPurchaseAmountAndBuyCosts()
    {
        // 100 shares for EUR 4,000 all-in, of which EUR 14.95 broker fee + EUR 14.00 TOB were
        // acquisition costs. The buy side is itemised the same way the exit side is.
        var txs = new ITransactionRepositoryFake([
            Buy(100m, 4_000m, "EUR", buyBrokerFee: 14.95m, buyTob: 14.00m),
        ]);
        var svc = Service(txs, PriceOf(40m, "EUR"), new FxFake(1m));

        var h = (await svc.GetHoldingsAsync(PortfolioId))[0];

        h.BuyCostsEur.Should().Be(28.95m);
        h.PurchaseAmountEur.Should().Be(4_000m - 28.95m);
        // The split must reconcile: the two parts are the cost basis, not extra deductions.
        (h.PurchaseAmountEur + h.BuyCostsEur).Should().Be(h.TotalInvestedEur);
        h.UnrealizedGainEur.Should().Be(4_000m - 28.95m - 4_000m);
    }

    [Fact]
    public async Task GetHoldings_ProRatesBuyCostsAcrossLotsAndPartiallySoldQuantities()
    {
        // Two lots at different fee levels, the first half sold off. Both the purchase amount
        // and the buy costs must be pro-rated by what is left, and still sum to the basis.
        var partlySold = Buy(100m, 2_000m, "EUR", buyBrokerFee: 14.95m, buyTob: 5.05m);
        partlySold.RemainingQuantity = 40m;
        var whole = Buy(50m, 1_000m, "EUR", buyBrokerFee: 5.95m, buyTob: 4.05m);
        var svc = Service(new ITransactionRepositoryFake([partlySold, whole]), PriceOf(40m, "EUR"), new FxFake(1m));

        var h = (await svc.GetHoldingsAsync(PortfolioId))[0];

        h.OpenQuantity.Should().Be(90m);
        h.BuyCostsEur.Should().Be(20m * 0.4m + 10m);              // 40% of the first lot, all of the second
        h.PurchaseAmountEur.Should().Be(1_980m * 0.4m + 990m);
        (h.PurchaseAmountEur + h.BuyCostsEur).Should().Be(h.TotalInvestedEur);
    }

    [Fact]
    public async Task GetHoldings_ExposesBuyCosts_EvenWithoutAPrice()
    {
        // The buy side is history, not a valuation, so it is known before any price refresh.
        var txs = new ITransactionRepositoryFake([
            Buy(10m, 1_000m, "EUR", buyBrokerFee: 5.95m, buyTob: 3.50m),
        ]);
        var svc = Service(txs, new IInstrumentPriceRepositoryFake(), new FxFake(1m));

        var h = (await svc.GetHoldingsAsync(PortfolioId))[0];

        h.MarketValueEur.Should().BeNull();
        h.BuyCostsEur.Should().Be(9.45m);
        h.PurchaseAmountEur.Should().Be(990.55m);
    }

    [Fact]
    public async Task GetHoldings_ExitCostsUseTheCurrentMarketValue_NotTheBuyPrice()
    {
        // Bought 100 shares at EUR 10 (EUR 1,000), now worth EUR 40 each (EUR 4,000).
        // The sell order is priced off the 4,000, not the 1,000 that was invested.
        var txs = new ITransactionRepositoryFake([Buy(100m, 1_000m, "EUR")]);
        var svc = Service(txs, PriceOf(40m, "EUR"), new FxFake(1m));

        var h = (await svc.GetHoldingsAsync(PortfolioId))[0];

        h.EstimatedSellCostsEur.Should().Be(14.95m + 14.00m); // 4,000 tier, not the 1,000 tier
        h.UnrealizedGainEur.Should().Be(4_000m - 28.95m - 1_000m);
    }

    [Fact]
    public async Task GetHoldings_ExitCostsAreComputedOnTheEurValue_ForForeignCurrencyPositions()
    {
        // 100 shares @ $50 = $5,000; 1 EUR = 1.25 USD -> EUR 4,000 order value.
        var txs = new ITransactionRepositoryFake([Buy(100m, 3_000m, "USD")]);
        var svc = Service(txs, PriceOf(50m, "USD"), new FxFake(1.25m));

        var h = (await svc.GetHoldingsAsync(PortfolioId))[0];

        h.MarketValueEur.Should().BeApproximately(4_000m, 0.001m);
        h.EstimatedSellCostsEur.Should().BeApproximately(14.95m + 14.00m, 0.001m);
        h.UnrealizedGainEur.Should().BeApproximately(4_000m - 28.95m - 3_000m, 0.001m);

        // The tooltip shows the order in EUR, so the unit price must be the converted one ($50 / 1.25).
        h.ExitCostOrders.Should().ContainSingle();
        h.ExitCostOrders![0].UnitPriceEur.Should().BeApproximately(40m, 0.001m);
        h.ExitCostOrders![0].OrderValueEur.Should().BeApproximately(4_000m, 0.001m);
    }

    [Fact]
    public async Task GetHoldings_ExitCostsFollowTheInstrumentType()
    {
        // MeDirect sells ETFs for free; only the ETF TOB (0.12%, under its cap here) remains.
        var txs = new ITransactionRepositoryFake([
            Buy(100m, 3_500m, "EUR", Broker.MeDirect, InstrumentType.Etf),
        ]);
        var svc = Service(txs, PriceOf(40m, "EUR"), new FxFake(1m));

        var h = (await svc.GetHoldingsAsync(PortfolioId))[0];

        h.EstimatedSellCostsEur.Should().Be(4.80m);
        h.UnrealizedGainEur.Should().Be(4_000m - 4.80m - 3_500m);
    }

    [Fact]
    public async Task GetHoldings_ExitCostsIgnoreLotsAlreadySoldOff()
    {
        // The Revolut lot has been fully consumed by an earlier sell, so no Revolut order
        // is priced, and it contributes neither quantity nor cost basis.
        var keytrade = Buy(100m, 1_000m, "EUR", Broker.Keytrade);
        var soldOff = Buy(50m, 500m, "EUR", Broker.Revolut);
        soldOff.RemainingQuantity = 0m;
        var svc = Service(new ITransactionRepositoryFake([keytrade, soldOff]), PriceOf(40m, "EUR"), new FxFake(1m));

        var h = (await svc.GetHoldingsAsync(PortfolioId))[0];

        h.OpenQuantity.Should().Be(100m);
        h.EstimatedSellCostsEur.Should().Be(14.95m + 14.00m);
    }

    [Fact]
    public async Task GetHoldings_ExitCostsArePartiallyChargedOnPartiallySoldLots()
    {
        // A 100-share Keytrade lot with 40 left: the order is 40 x EUR 40 = EUR 1,600, and the
        // cost basis is pro-rated to 40% of the original EUR 2,000.
        var partial = Buy(100m, 2_000m, "EUR");
        partial.RemainingQuantity = 40m;
        var svc = Service(new ITransactionRepositoryFake([partial]), PriceOf(40m, "EUR"), new FxFake(1m));

        var h = (await svc.GetHoldingsAsync(PortfolioId))[0];

        h.TotalInvestedEur.Should().Be(800m);
        h.MarketValueEur.Should().Be(1_600m);
        h.EstimatedSellCostsEur.Should().Be(5.95m + 5.60m); // 1,600 -> Keytrade <= 2,500 tier
        h.UnrealizedGainEur.Should().Be(1_600m - 11.55m - 800m);
    }

    [Fact]
    public async Task GetHoldings_LeavesValuationNull_WhenNoCachedPrice()
    {
        var txs = new ITransactionRepositoryFake([Buy(5m, 500m, "EUR")]);
        var svc = Service(txs, new IInstrumentPriceRepositoryFake(), new FxFake(1m));

        var holdings = await svc.GetHoldingsAsync(PortfolioId);

        holdings.Should().HaveCount(1);
        holdings[0].MarketValueEur.Should().BeNull();
        holdings[0].EstimatedSellCostsEur.Should().BeNull();
        holdings[0].ExitCostOrders.Should().BeNull();
        holdings[0].UnrealizedGainEur.Should().BeNull();
        holdings[0].PriceAsOf.Should().BeNull();
        holdings[0].PriceFetchedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetHoldings_LeavesValuationNull_WhenFxRateUnknown()
    {
        var txs = new ITransactionRepositoryFake([Buy(10m, 1000m, "USD")]);
        var svc = Service(txs, PriceOf(200m, "USD"), new FxFake(0m)); // 0 = unknown

        var holdings = await svc.GetHoldingsAsync(PortfolioId);

        holdings[0].CurrentPriceNative.Should().Be(200m); // price still surfaced
        holdings[0].MarketValueEur.Should().BeNull();      // but no EUR conversion
        holdings[0].EstimatedSellCostsEur.Should().BeNull();
        holdings[0].ExitCostOrders.Should().BeNull();
        holdings[0].UnrealizedGainEur.Should().BeNull();
    }

    // --- Fakes ---

    private sealed class FxFake(decimal rate) : IFxRateProvider
    {
        public Task<decimal> GetEurRateAsync(string currency, CancellationToken ct = default)
            => Task.FromResult(currency == "EUR" ? 1m : rate);
        public Task<IReadOnlyDictionary<DateOnly, decimal>> GetEurRateHistoryAsync(
            string currency, DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<DateOnly, decimal>>(new Dictionary<DateOnly, decimal>());
    }

    private sealed class ITransactionRepositoryFake(IReadOnlyList<Transaction> txs) : ITransactionRepository
    {
        public Task<IReadOnlyList<Transaction>> GetByPortfolioAsync(Guid portfolioId, CancellationToken ct = default)
            => Task.FromResult(txs);
        public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Transaction?>(null);
        public Task<IReadOnlyList<Transaction>> GetOpenBuyLotsAsync(Guid portfolioId, Guid instrumentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Transaction>>([]);
        public Task<IReadOnlyList<SaleAllocation>> GetAllocationsAsync(Guid portfolioId, int? year, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SaleAllocation>>([]);
        public Task<IReadOnlyList<Transaction>> GetByPortfolioAndInstrumentAsync(Guid portfolioId, Guid instrumentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Transaction>>(txs.Where(t => t.PortfolioId == portfolioId && t.InstrumentId == instrumentId).ToList());
        public Task RemoveAllocationsForSellsAsync(IEnumerable<Guid> sellTransactionIds, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddAsync(Transaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddAllocationsAsync(IEnumerable<SaleAllocation> allocations, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Transaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Transaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class IInstrumentPriceRepositoryFake(params InstrumentPrice[] prices) : IInstrumentPriceRepository
    {
        public Task<IReadOnlyList<InstrumentPrice>> GetByPortfolioAsync(Guid portfolioId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InstrumentPrice>>(prices);
        public Task<InstrumentPrice?> GetByInstrumentAsync(Guid instrumentId, CancellationToken ct = default)
            => Task.FromResult(prices.FirstOrDefault(p => p.InstrumentId == instrumentId));
        public Task UpsertAsync(InstrumentPrice price, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class IInstrumentRepositoryFake : IInstrumentRepository
    {
        public Task<Instrument?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Instrument?>(null);
        public Task<Instrument?> GetByIsinAsync(string isin, CancellationToken ct = default) => Task.FromResult<Instrument?>(null);
        public Task<IReadOnlyList<Instrument>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Instrument>>([]);
        public Task AddAsync(Instrument instrument, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
