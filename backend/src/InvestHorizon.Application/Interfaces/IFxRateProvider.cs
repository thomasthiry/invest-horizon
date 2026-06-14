namespace InvestHorizon.Application.Interfaces;

/// <summary>Provides current FX rates relative to EUR (the reporting currency).</summary>
public interface IFxRateProvider
{
    /// <summary>
    /// Returns the rate "1 EUR = x <paramref name="currency"/>" (EUR returns 1).
    /// To convert a native amount to EUR, divide by this rate.
    /// </summary>
    Task<decimal> GetEurRateAsync(string currency, CancellationToken ct = default);

    /// <summary>
    /// Returns daily "1 EUR = x <paramref name="currency"/>" rates over the inclusive
    /// <paramref name="from"/>..<paramref name="to"/> range, keyed by date. EUR returns an empty map
    /// (rate is always 1); only days the provider publishes a rate are included.
    /// </summary>
    Task<IReadOnlyDictionary<DateOnly, decimal>> GetEurRateHistoryAsync(
        string currency, DateOnly from, DateOnly to, CancellationToken ct = default);
}
