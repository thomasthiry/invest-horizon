namespace InvestHorizon.Application.Interfaces;

/// <summary>
/// Provides monthly HICP inflation index data (2015 = 100) for a given region.
/// Implementations are expected to return all available months in one call.
/// </summary>
public interface IInflationProvider
{
    /// <summary>
    /// Returns the monthly HICP index keyed by the first day of each month.
    /// Returns an empty dictionary when the provider is unavailable.
    /// </summary>
    Task<IReadOnlyDictionary<DateOnly, decimal>> GetIndexHistoryAsync(
        string region, CancellationToken ct = default);
}
