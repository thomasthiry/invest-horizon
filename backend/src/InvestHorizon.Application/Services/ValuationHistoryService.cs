using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.Services;

/// <summary>
/// Reconstructs the portfolio's total EUR value for every day since the first transaction.
/// Positions per day are replayed from raw transaction quantities (not <c>RemainingQuantity</c>,
/// which is a current snapshot); daily prices and FX rates come from the DB cache, lazily topped
/// up from the external providers when the cache does not yet reach today.
/// </summary>
public sealed class ValuationHistoryService
{
    private readonly ITransactionRepository _transactions;
    private readonly IInstrumentRepository _instruments;
    private readonly IPriceProvider _priceProvider;
    private readonly IFxRateProvider _fxProvider;
    private readonly IInstrumentPriceHistoryRepository _priceHistory;
    private readonly IFxRateHistoryRepository _fxHistory;

    public ValuationHistoryService(
        ITransactionRepository transactions,
        IInstrumentRepository instruments,
        IPriceProvider priceProvider,
        IFxRateProvider fxProvider,
        IInstrumentPriceHistoryRepository priceHistory,
        IFxRateHistoryRepository fxHistory)
    {
        _transactions = transactions;
        _instruments = instruments;
        _priceProvider = priceProvider;
        _fxProvider = fxProvider;
        _priceHistory = priceHistory;
        _fxHistory = fxHistory;
    }

    public async Task<IReadOnlyList<ValuationPoint>> GetAsync(Guid portfolioId, CancellationToken ct = default)
    {
        var txs = await _transactions.GetByPortfolioAsync(portfolioId, ct);
        if (txs.Count == 0) return Array.Empty<ValuationPoint>();

        var from = txs.Min(t => t.Date);
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var maxTxDate = txs.Max(t => t.Date);
        if (maxTxDate > to) to = maxTxDate;

        // Net quantity per instrument as a step function over time.
        var qtyByInstrument = new Dictionary<Guid, StepSeries>();
        foreach (var g in txs.GroupBy(t => t.InstrumentId))
            qtyByInstrument[g.Key] = StepSeries.Cumulative(
                g.Select(t => (t.Date, t.Side == TransactionSide.Buy ? t.Quantity : -t.Quantity)));

        // Net invested EUR (buy cost − sell proceeds) across the whole portfolio.
        var investedSeries = StepSeries.Cumulative(
            txs.Select(t => (t.Date, t.Side == TransactionSide.Buy ? t.TotalCost : -t.NetProceeds)));

        // Cached daily close per held instrument; currency is taken from the price points themselves.
        var priceByInstrument = new Dictionary<Guid, StepSeries>();
        var currencyByInstrument = new Dictionary<Guid, string>();
        foreach (var instrumentId in qtyByInstrument.Keys)
        {
            var (series, currency) = await EnsurePriceHistoryAsync(instrumentId, from, to, ct);
            priceByInstrument[instrumentId] = series;
            currencyByInstrument[instrumentId] = currency;
        }

        // FX series for every non-EUR currency that appears.
        var fxByCurrency = new Dictionary<string, StepSeries>(StringComparer.OrdinalIgnoreCase);
        foreach (var ccy in currencyByInstrument.Values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(ccy) || ccy.Equals("EUR", StringComparison.OrdinalIgnoreCase))
                continue;
            fxByCurrency[ccy] = await EnsureFxHistoryAsync(ccy, from, to, ct);
        }

        var points = new List<ValuationPoint>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            decimal valueEur = 0m;
            foreach (var (instrumentId, qtySeries) in qtyByInstrument)
            {
                var qty = qtySeries.ValueAt(d) ?? 0m;
                if (qty == 0m) continue;

                var price = priceByInstrument[instrumentId].ValueAt(d, orEarliest: true);
                if (price is null) continue; // no price known yet → not valued

                var ccy = currencyByInstrument[instrumentId];
                if (string.IsNullOrWhiteSpace(ccy) || ccy.Equals("EUR", StringComparison.OrdinalIgnoreCase))
                {
                    valueEur += qty * price.Value;
                }
                else if (fxByCurrency.TryGetValue(ccy, out var fxSeries) &&
                         fxSeries.ValueAt(d, orEarliest: true) is { } rate && rate > 0m)
                {
                    valueEur += qty * price.Value / rate;
                }
            }

            points.Add(new ValuationPoint(d, decimal.Round(valueEur, 2), decimal.Round(investedSeries.ValueAt(d) ?? 0m, 2)));
        }

        return points;
    }

    private async Task<(StepSeries Series, string Currency)> EnsurePriceHistoryAsync(
        Guid instrumentId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var latest = await _priceHistory.GetLatestDateAsync(instrumentId, ct);
        var fetchFrom = latest is null ? from : latest.Value.AddDays(1);
        if (fetchFrom <= to)
        {
            var instrument = await _instruments.GetByIdAsync(instrumentId, ct);
            if (instrument is not null)
            {
                var fetched = await _priceProvider.GetHistoryAsync(instrument, fetchFrom, to, ct);
                if (fetched.Count > 0)
                    await _priceHistory.UpsertRangeAsync(fetched.Select(p => new InstrumentPriceHistory
                    {
                        InstrumentId = instrumentId,
                        Date = p.Date,
                        CloseNative = p.CloseNative,
                        Currency = p.Currency
                    }), ct);
            }
        }

        var rows = await _priceHistory.GetRangeAsync(instrumentId, from, to, ct);
        var currency = rows.Count > 0 ? rows[^1].Currency : "EUR";
        return (StepSeries.FromValues(rows.Select(r => (r.Date, r.CloseNative))), currency);
    }

    private async Task<StepSeries> EnsureFxHistoryAsync(string currency, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var latest = await _fxHistory.GetLatestDateAsync(currency, ct);
        var fetchFrom = latest is null ? from : latest.Value.AddDays(1);
        if (fetchFrom <= to)
        {
            var fetched = await _fxProvider.GetEurRateHistoryAsync(currency, fetchFrom, to, ct);
            if (fetched.Count > 0)
                await _fxHistory.UpsertRangeAsync(fetched.Select(kv => new FxRateHistory
                {
                    Currency = currency,
                    Date = kv.Key,
                    RatePerEur = kv.Value
                }), ct);
        }

        var rows = await _fxHistory.GetRangeAsync(currency, from, to, ct);
        return StepSeries.FromValues(rows.Select(r => (r.Date, r.RatePerEur)));
    }

    /// <summary>
    /// A step function over dates: <see cref="ValueAt"/> returns the value effective on a given day,
    /// i.e. the most recent entry with date ≤ the query (forward-fill).
    /// </summary>
    private sealed class StepSeries
    {
        private readonly DateOnly[] _dates;
        private readonly decimal[] _values;

        private StepSeries(DateOnly[] dates, decimal[] values)
        {
            _dates = dates;
            _values = values;
        }

        /// <summary>Builds a step series directly from (date, value) pairs (e.g. daily closes).</summary>
        public static StepSeries FromValues(IEnumerable<(DateOnly Date, decimal Value)> points)
        {
            var ordered = points.OrderBy(p => p.Date).ToArray();
            return new StepSeries(ordered.Select(p => p.Date).ToArray(), ordered.Select(p => p.Value).ToArray());
        }

        /// <summary>Builds a running-total step series from (date, delta) pairs, summing same-day deltas.</summary>
        public static StepSeries Cumulative(IEnumerable<(DateOnly Date, decimal Delta)> deltas)
        {
            var byDate = deltas.GroupBy(d => d.Date)
                .OrderBy(g => g.Key)
                .Select(g => (Date: g.Key, Delta: g.Sum(x => x.Delta)))
                .ToArray();

            var dates = new DateOnly[byDate.Length];
            var values = new decimal[byDate.Length];
            decimal running = 0m;
            for (var i = 0; i < byDate.Length; i++)
            {
                running += byDate[i].Delta;
                dates[i] = byDate[i].Date;
                values[i] = running;
            }

            return new StepSeries(dates, values);
        }

        /// <summary>
        /// Value effective on <paramref name="date"/> (most recent entry with date ≤ it). When none exists
        /// and <paramref name="orEarliest"/> is set, falls back to the earliest entry; otherwise null.
        /// </summary>
        public decimal? ValueAt(DateOnly date, bool orEarliest = false)
        {
            if (_dates.Length == 0) return null;

            // Binary search for the last index with _dates[i] <= date.
            int lo = 0, hi = _dates.Length - 1, found = -1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                if (_dates[mid] <= date) { found = mid; lo = mid + 1; }
                else hi = mid - 1;
            }

            if (found >= 0) return _values[found];
            return orEarliest ? _values[0] : null;
        }
    }
}

/// <summary>A single day on the portfolio value curve, all figures in EUR.</summary>
public record ValuationPoint(DateOnly Date, decimal ValueEur, decimal InvestedEur);
