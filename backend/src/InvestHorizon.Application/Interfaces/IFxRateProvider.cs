namespace InvestHorizon.Application.Interfaces;

/// <summary>Provides current FX rates relative to EUR (the reporting currency).</summary>
public interface IFxRateProvider
{
    /// <summary>
    /// Returns the rate "1 EUR = x <paramref name="currency"/>" (EUR returns 1).
    /// To convert a native amount to EUR, divide by this rate.
    /// </summary>
    Task<decimal> GetEurRateAsync(string currency, CancellationToken ct = default);
}
