using InvestHorizon.Domain.Entities;

namespace InvestHorizon.Application.Interfaces;

public interface IInflationHistoryRepository
{
    /// <summary>Cached monthly HICP rows for a region over the inclusive date range, ordered by date.</summary>
    Task<IReadOnlyList<InflationHistory>> GetRangeAsync(
        string region, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Most recent cached month (first-of-month date) for a region, or null if nothing is cached.</summary>
    Task<DateOnly?> GetLatestDateAsync(string region, CancellationToken ct = default);

    /// <summary>Insert or overwrite the given monthly rows, then persist.</summary>
    Task UpsertRangeAsync(IEnumerable<InflationHistory> rows, CancellationToken ct = default);
}
