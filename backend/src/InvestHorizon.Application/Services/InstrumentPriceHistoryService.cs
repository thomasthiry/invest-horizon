using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;

namespace InvestHorizon.Application.Services;

/// <summary>
/// Returns daily closing prices for a single instrument, lazily fetching any gap
/// between the newest cached date and today from the external price provider.
/// </summary>
public sealed class InstrumentPriceHistoryService
{
    private readonly IInstrumentRepository _instruments;
    private readonly IPriceProvider _priceProvider;
    private readonly IInstrumentPriceHistoryRepository _priceHistory;

    public InstrumentPriceHistoryService(
        IInstrumentRepository instruments,
        IPriceProvider priceProvider,
        IInstrumentPriceHistoryRepository priceHistory)
    {
        _instruments = instruments;
        _priceProvider = priceProvider;
        _priceHistory = priceHistory;
    }

    public async Task<IReadOnlyList<InstrumentPriceHistory>> GetAsync(
        Guid instrumentId, DateOnly from, DateOnly to, CancellationToken ct = default)
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

        return await _priceHistory.GetRangeAsync(instrumentId, from, to, ct);
    }
}
