using InvestHorizon.Domain.Entities;

namespace InvestHorizon.Application.Interfaces;

public interface IInstrumentPriceHistoryRepository
{
    /// <summary>Cached daily closes for an instrument over the inclusive date range, ordered by date.</summary>
    Task<IReadOnlyList<InstrumentPriceHistory>> GetRangeAsync(
        Guid instrumentId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Most recent cached date for an instrument, or null if nothing is cached.</summary>
    Task<DateOnly?> GetLatestDateAsync(Guid instrumentId, CancellationToken ct = default);

    /// <summary>Insert or overwrite the given daily closes, then persist.</summary>
    Task UpsertRangeAsync(IEnumerable<InstrumentPriceHistory> points, CancellationToken ct = default);
}
