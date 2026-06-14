using InvestHorizon.Domain.Entities;

namespace InvestHorizon.Application.Interfaces;

public interface IFxRateHistoryRepository
{
    /// <summary>Cached daily EUR rates for a currency over the inclusive date range, ordered by date.</summary>
    Task<IReadOnlyList<FxRateHistory>> GetRangeAsync(
        string currency, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Most recent cached date for a currency, or null if nothing is cached.</summary>
    Task<DateOnly?> GetLatestDateAsync(string currency, CancellationToken ct = default);

    /// <summary>Insert or overwrite the given daily rates, then persist.</summary>
    Task UpsertRangeAsync(IEnumerable<FxRateHistory> rates, CancellationToken ct = default);
}
