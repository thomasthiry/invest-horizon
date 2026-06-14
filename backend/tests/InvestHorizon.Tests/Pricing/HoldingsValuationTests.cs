using FluentAssertions;
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

    private static Transaction Buy(decimal qty, decimal totalCostEur, string currency) =>
        new()
        {
            Id = Guid.NewGuid(),
            PortfolioId = PortfolioId,
            InstrumentId = InstrumentId,
            Instrument = new Instrument { Id = InstrumentId, Isin = "US0378331005", Name = "Apple", Currency = currency },
            Side = TransactionSide.Buy,
            Currency = currency,
            Quantity = qty,
            RemainingQuantity = qty,
            TotalCost = totalCostEur,
        };

    [Fact]
    public async Task GetHoldings_ComputesMarketValueAndUnrealizedInEur()
    {
        // 10 shares bought for €1000 total; now $200 each, 1 EUR = 1.25 USD.
        var txs = new ITransactionRepositoryFake([Buy(10m, 1000m, "USD")]);
        var asOf = new DateTime(2026, 6, 12, 17, 0, 0, DateTimeKind.Utc);
        var fetchedAt = new DateTime(2026, 6, 13, 9, 42, 0, DateTimeKind.Utc);
        var prices = new IInstrumentPriceRepositoryFake(new InstrumentPrice
        {
            InstrumentId = InstrumentId,
            PriceNative = 200m,
            Currency = "USD",
            AsOf = asOf,
            FetchedAt = fetchedAt,
            Source = "Yahoo",
        });
        var svc = new HoldingsService(txs, new IInstrumentRepositoryFake(), prices, new FxFake(1.25m));

        var holdings = await svc.GetHoldingsAsync(PortfolioId);

        holdings.Should().HaveCount(1);
        var h = holdings[0];
        // 10 * 200 / 1.25 = 1600 EUR
        h.MarketValueEur.Should().BeApproximately(1600m, 0.001m);
        h.UnrealizedGainEur.Should().BeApproximately(600m, 0.001m);
        h.CurrentPriceNative.Should().Be(200m);
        h.PriceCurrency.Should().Be("USD");
        h.PriceAsOf.Should().Be(asOf);
        h.PriceFetchedAt.Should().Be(fetchedAt);
    }

    [Fact]
    public async Task GetHoldings_LeavesValuationNull_WhenNoCachedPrice()
    {
        var txs = new ITransactionRepositoryFake([Buy(5m, 500m, "EUR")]);
        var svc = new HoldingsService(txs, new IInstrumentRepositoryFake(), new IInstrumentPriceRepositoryFake(), new FxFake(1m));

        var holdings = await svc.GetHoldingsAsync(PortfolioId);

        holdings.Should().HaveCount(1);
        holdings[0].MarketValueEur.Should().BeNull();
        holdings[0].UnrealizedGainEur.Should().BeNull();
        holdings[0].PriceAsOf.Should().BeNull();
        holdings[0].PriceFetchedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetHoldings_LeavesValuationNull_WhenFxRateUnknown()
    {
        var txs = new ITransactionRepositoryFake([Buy(10m, 1000m, "USD")]);
        var prices = new IInstrumentPriceRepositoryFake(new InstrumentPrice
        {
            InstrumentId = InstrumentId, PriceNative = 200m, Currency = "USD", Source = "Yahoo",
        });
        var svc = new HoldingsService(txs, new IInstrumentRepositoryFake(), prices, new FxFake(0m)); // 0 = unknown

        var holdings = await svc.GetHoldingsAsync(PortfolioId);

        holdings[0].CurrentPriceNative.Should().Be(200m); // price still surfaced
        holdings[0].MarketValueEur.Should().BeNull();      // but no EUR conversion
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
