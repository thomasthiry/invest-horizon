using FluentAssertions;
using InvestHorizon.Application.Interfaces;
using InvestHorizon.Application.Services;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;
using Xunit;

namespace InvestHorizon.Tests.Pricing;

public class ValuationHistoryServiceTests
{
    private static readonly Guid PortfolioId = Guid.NewGuid();

    private static Transaction Tx(Guid instrumentId, TransactionSide side, DateOnly date,
        decimal qty, decimal totalCost, decimal netProceeds, string currency) =>
        new()
        {
            Id = Guid.NewGuid(),
            PortfolioId = PortfolioId,
            InstrumentId = instrumentId,
            Side = side,
            Date = date,
            Currency = currency,
            Quantity = qty,
            RemainingQuantity = side == TransactionSide.Buy ? qty : 0m,
            TotalCost = totalCost,
            NetProceeds = netProceeds,
        };

    private static ValuationHistoryService Build(
        IReadOnlyList<Transaction> txs,
        IReadOnlyDictionary<Guid, IReadOnlyList<PriceHistoryPoint>> prices,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateOnly, decimal>>? fx = null,
        IReadOnlyList<InstrumentPrice>? liveQuotes = null,
        IReadOnlyDictionary<string, decimal>? liveFxRates = null)
    {
        var instruments = txs.Select(t => t.InstrumentId).Distinct()
            .ToDictionary(id => id, id => new Instrument { Id = id, Isin = "X", Name = "X", Currency = "EUR" });
        return new ValuationHistoryService(
            new TxRepoFake(txs),
            new InstrumentRepoFake(instruments),
            new PriceProviderFake(prices),
            new FxProviderFake(fx ?? new Dictionary<string, IReadOnlyDictionary<DateOnly, decimal>>(), liveFxRates),
            new PriceHistoryRepoFake(),
            new FxHistoryRepoFake(),
            new LivePriceRepoFake(liveQuotes ?? []),
            new InflationProviderFake(),
            new InflationHistoryRepoFake());
    }

    private static decimal ValueOn(IReadOnlyList<ValuationPoint> points, DateOnly date)
        => points.Single(p => p.Date == date).ValueEur;

    private static decimal InvestedOn(IReadOnlyList<ValuationPoint> points, DateOnly date)
        => points.Single(p => p.Date == date).InvestedEur;

    [Fact]
    public async Task EmptyTransactions_ReturnsEmpty()
    {
        var svc = Build([], new Dictionary<Guid, IReadOnlyList<PriceHistoryPoint>>());
        (await svc.GetAsync(PortfolioId)).Should().BeEmpty();
    }

    [Fact]
    public async Task BuyThenPartialSell_ValuesNetQuantityWithForwardFill()
    {
        var i1 = Guid.NewGuid();
        var txs = new[]
        {
            Tx(i1, TransactionSide.Buy, new DateOnly(2024, 1, 1), 10m, 1000m, 0m, "EUR"),
            Tx(i1, TransactionSide.Sell, new DateOnly(2024, 1, 3), 4m, 0m, 480m, "EUR"),
        };
        var prices = new Dictionary<Guid, IReadOnlyList<PriceHistoryPoint>>
        {
            [i1] =
            [
                new(new DateOnly(2024, 1, 1), 100m, "EUR"),
                new(new DateOnly(2024, 1, 2), 110m, "EUR"),
                new(new DateOnly(2024, 1, 3), 120m, "EUR"),
                // 2024-01-04 missing (weekend) → forward-fill 120
                new(new DateOnly(2024, 1, 5), 130m, "EUR"),
            ],
        };
        var svc = Build(txs, prices);

        var points = await svc.GetAsync(PortfolioId);

        points[0].Date.Should().Be(new DateOnly(2024, 1, 1));
        ValueOn(points, new DateOnly(2024, 1, 1)).Should().Be(1000m); // 10 * 100
        ValueOn(points, new DateOnly(2024, 1, 2)).Should().Be(1100m); // 10 * 110
        ValueOn(points, new DateOnly(2024, 1, 3)).Should().Be(720m);  // (10-4) * 120
        ValueOn(points, new DateOnly(2024, 1, 4)).Should().Be(720m);  // forward-filled price 120
        ValueOn(points, new DateOnly(2024, 1, 5)).Should().Be(780m);  // 6 * 130

        InvestedOn(points, new DateOnly(2024, 1, 1)).Should().Be(1000m);
        InvestedOn(points, new DateOnly(2024, 1, 3)).Should().Be(520m); // 1000 - 480
    }

    [Fact]
    public async Task NonEurInstrument_ConvertsViaFxRate()
    {
        var i1 = Guid.NewGuid();
        var txs = new[] { Tx(i1, TransactionSide.Buy, new DateOnly(2024, 2, 1), 10m, 1600m, 0m, "USD") };
        var prices = new Dictionary<Guid, IReadOnlyList<PriceHistoryPoint>>
        {
            [i1] = [new(new DateOnly(2024, 2, 1), 200m, "USD")],
        };
        var fx = new Dictionary<string, IReadOnlyDictionary<DateOnly, decimal>>
        {
            ["USD"] = new Dictionary<DateOnly, decimal> { [new DateOnly(2024, 2, 1)] = 1.25m },
        };
        var svc = Build(txs, prices, fx);

        var points = await svc.GetAsync(PortfolioId);

        ValueOn(points, new DateOnly(2024, 2, 1)).Should().Be(1600m); // 10 * 200 / 1.25
    }

    [Fact]
    public async Task TodaysPoint_UsesLiveQuoteWhenAvailable_MatchingHoldingsTotal()
    {
        var i1 = Guid.NewGuid();
        var pastDate = new DateOnly(2024, 3, 1);
        var txs = new[] { Tx(i1, TransactionSide.Buy, pastDate, 10m, 1000m, 0m, "EUR") };

        // Daily close history has 100 EUR; live quote has 150 EUR.
        var prices = new Dictionary<Guid, IReadOnlyList<PriceHistoryPoint>>
        {
            [i1] = [new(pastDate, 100m, "EUR")],
        };
        var liveQuotes = new[]
        {
            new InstrumentPrice { InstrumentId = i1, PriceNative = 150m, Currency = "EUR" },
        };
        var svc = Build(txs, prices, liveQuotes: liveQuotes);

        var points = await svc.GetAsync(PortfolioId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Past days still use daily-close (100 × 10 = 1000).
        ValueOn(points, pastDate).Should().Be(1000m);
        // Today uses live quote (150 × 10 = 1500).
        ValueOn(points, today).Should().Be(1500m);
    }

    [Fact]
    public async Task TodaysPoint_UsesLiveQuoteWithFxConversion()
    {
        var i1 = Guid.NewGuid();
        var pastDate = new DateOnly(2024, 4, 1);
        var txs = new[] { Tx(i1, TransactionSide.Buy, pastDate, 10m, 2000m, 0m, "USD") };

        var prices = new Dictionary<Guid, IReadOnlyList<PriceHistoryPoint>>
        {
            [i1] = [new(pastDate, 200m, "USD")],
        };
        var fxHistory = new Dictionary<string, IReadOnlyDictionary<DateOnly, decimal>>
        {
            ["USD"] = new Dictionary<DateOnly, decimal> { [pastDate] = 1.10m },
        };
        var liveQuotes = new[]
        {
            // Live price = 210 USD; live FX = 1 EUR = 1.20 USD → 10 * 210 / 1.20 = 1750
            new InstrumentPrice { InstrumentId = i1, PriceNative = 210m, Currency = "USD" },
        };
        var liveFxRates = new Dictionary<string, decimal> { ["USD"] = 1.20m };
        var svc = Build(txs, prices, fxHistory, liveQuotes, liveFxRates);

        var points = await svc.GetAsync(PortfolioId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        ValueOn(points, today).Should().Be(1750m); // 10 * 210 / 1.20
    }

    [Fact]
    public async Task TodaysPoint_FallsBackToDailyClose_WhenNoLiveQuote()
    {
        var i1 = Guid.NewGuid();
        var pastDate = new DateOnly(2024, 5, 1);
        var txs = new[] { Tx(i1, TransactionSide.Buy, pastDate, 5m, 500m, 0m, "EUR") };
        var prices = new Dictionary<Guid, IReadOnlyList<PriceHistoryPoint>>
        {
            [i1] = [new(pastDate, 100m, "EUR")],
        };
        // No live quotes provided.
        var svc = Build(txs, prices);

        var points = await svc.GetAsync(PortfolioId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Falls back to forward-filled close: 5 * 100 = 500.
        ValueOn(points, today).Should().Be(500m);
    }

    // --- Fakes ---

    private sealed class PriceProviderFake(IReadOnlyDictionary<Guid, IReadOnlyList<PriceHistoryPoint>> prices) : IPriceProvider
    {
        public Task<PriceQuote?> GetQuoteAsync(Instrument instrument, CancellationToken ct = default)
            => Task.FromResult<PriceQuote?>(null);
        public Task<IReadOnlyList<PriceHistoryPoint>> GetHistoryAsync(Instrument instrument, DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult(prices.TryGetValue(instrument.Id, out var p)
                ? (IReadOnlyList<PriceHistoryPoint>)p.Where(x => x.Date >= from && x.Date <= to).ToList()
                : Array.Empty<PriceHistoryPoint>());
    }

    private sealed class FxProviderFake(
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateOnly, decimal>> fx,
        IReadOnlyDictionary<string, decimal>? liveRates = null) : IFxRateProvider
    {
        public Task<decimal> GetEurRateAsync(string currency, CancellationToken ct = default)
            => Task.FromResult(liveRates is not null && liveRates.TryGetValue(currency, out var r) ? r : 1m);
        public Task<IReadOnlyDictionary<DateOnly, decimal>> GetEurRateHistoryAsync(string currency, DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult(fx.TryGetValue(currency, out var r)
                ? (IReadOnlyDictionary<DateOnly, decimal>)r.Where(kv => kv.Key >= from && kv.Key <= to).ToDictionary(kv => kv.Key, kv => kv.Value)
                : new Dictionary<DateOnly, decimal>());
    }

    private sealed class LivePriceRepoFake(IReadOnlyList<InstrumentPrice> prices) : IInstrumentPriceRepository
    {
        public Task<IReadOnlyList<InstrumentPrice>> GetByPortfolioAsync(Guid portfolioId, CancellationToken ct = default)
            => Task.FromResult(prices);
        public Task<InstrumentPrice?> GetByInstrumentAsync(Guid instrumentId, CancellationToken ct = default)
            => Task.FromResult(prices.FirstOrDefault(p => p.InstrumentId == instrumentId));
        public Task UpsertAsync(InstrumentPrice price, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InstrumentRepoFake(IReadOnlyDictionary<Guid, Instrument> instruments) : IInstrumentRepository
    {
        public Task<Instrument?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(instruments.TryGetValue(id, out var i) ? i : null);
        public Task<Instrument?> GetByIsinAsync(string isin, CancellationToken ct = default) => Task.FromResult<Instrument?>(null);
        public Task<IReadOnlyList<Instrument>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Instrument>>([]);
        public Task AddAsync(Instrument instrument, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TxRepoFake(IReadOnlyList<Transaction> txs) : ITransactionRepository
    {
        public Task<IReadOnlyList<Transaction>> GetByPortfolioAsync(Guid portfolioId, CancellationToken ct = default) => Task.FromResult(txs);
        public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Transaction?>(null);
        public Task<IReadOnlyList<Transaction>> GetOpenBuyLotsAsync(Guid portfolioId, Guid instrumentId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Transaction>>([]);
        public Task<IReadOnlyList<SaleAllocation>> GetAllocationsAsync(Guid portfolioId, int? year, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SaleAllocation>>([]);
        public Task<IReadOnlyList<Transaction>> GetByPortfolioAndInstrumentAsync(Guid portfolioId, Guid instrumentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Transaction>>(txs.Where(t => t.PortfolioId == portfolioId && t.InstrumentId == instrumentId).ToList());
        public Task RemoveAllocationsForSellsAsync(IEnumerable<Guid> sellTransactionIds, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddAsync(Transaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddAllocationsAsync(IEnumerable<SaleAllocation> allocations, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Transaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Transaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class PriceHistoryRepoFake : IInstrumentPriceHistoryRepository
    {
        private readonly List<InstrumentPriceHistory> _rows = [];
        public Task<IReadOnlyList<InstrumentPriceHistory>> GetRangeAsync(Guid instrumentId, DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InstrumentPriceHistory>>(
                _rows.Where(r => r.InstrumentId == instrumentId && r.Date >= from && r.Date <= to).OrderBy(r => r.Date).ToList());
        public Task<DateOnly?> GetLatestDateAsync(Guid instrumentId, CancellationToken ct = default)
        {
            var dates = _rows.Where(r => r.InstrumentId == instrumentId).Select(r => r.Date).ToList();
            return Task.FromResult<DateOnly?>(dates.Count > 0 ? dates.Max() : null);
        }
        public Task UpsertRangeAsync(IEnumerable<InstrumentPriceHistory> points, CancellationToken ct = default)
        {
            foreach (var p in points)
            {
                _rows.RemoveAll(r => r.InstrumentId == p.InstrumentId && r.Date == p.Date);
                _rows.Add(p);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FxHistoryRepoFake : IFxRateHistoryRepository
    {
        private readonly List<FxRateHistory> _rows = [];
        public Task<IReadOnlyList<FxRateHistory>> GetRangeAsync(string currency, DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FxRateHistory>>(
                _rows.Where(r => r.Currency == currency && r.Date >= from && r.Date <= to).OrderBy(r => r.Date).ToList());
        public Task<DateOnly?> GetLatestDateAsync(string currency, CancellationToken ct = default)
        {
            var dates = _rows.Where(r => r.Currency == currency).Select(r => r.Date).ToList();
            return Task.FromResult<DateOnly?>(dates.Count > 0 ? dates.Max() : null);
        }
        public Task UpsertRangeAsync(IEnumerable<FxRateHistory> rates, CancellationToken ct = default)
        {
            foreach (var r in rates)
            {
                _rows.RemoveAll(x => x.Currency == r.Currency && x.Date == r.Date);
                _rows.Add(r);
            }
            return Task.CompletedTask;
        }
    }

    // Returns no CPI data so existing tests aren't affected by inflation math.
    private sealed class InflationProviderFake : IInflationProvider
    {
        public Task<IReadOnlyDictionary<DateOnly, decimal>> GetIndexHistoryAsync(
            string region, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<DateOnly, decimal>>(new Dictionary<DateOnly, decimal>());
    }

    private sealed class InflationHistoryRepoFake : IInflationHistoryRepository
    {
        public Task<IReadOnlyList<InflationHistory>> GetRangeAsync(string region, DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InflationHistory>>([]);
        public Task<DateOnly?> GetLatestDateAsync(string region, CancellationToken ct = default)
            => Task.FromResult<DateOnly?>(null);
        public Task UpsertRangeAsync(IEnumerable<InflationHistory> rows, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
