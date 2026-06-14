namespace InvestHorizon.Domain.Entities;

/// <summary>
/// Cached daily FX rate relative to EUR (one row per currency per day). A cache of external
/// time-series data; EUR is never stored (its rate is always 1).
/// </summary>
public class FxRateHistory
{
    /// <summary>ISO currency code, e.g. "USD".</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Day this rate belongs to.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Rate "1 EUR = x <see cref="Currency"/>". Divide a native amount by this to get EUR.</summary>
    public decimal RatePerEur { get; set; }
}
