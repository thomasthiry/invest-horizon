namespace InvestHorizon.Domain.Entities;

/// <summary>
/// Cached monthly HICP inflation index (one row per region per month).
/// Date is always the first day of the month; Index is 2015=100.
/// </summary>
public class InflationHistory
{
    /// <summary>ISO 3166-1 alpha-2 country code or Eurostat geo identifier (e.g. "BE").</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>First day of the CPI reference month (e.g. 2024-01-01 for January 2024).</summary>
    public DateOnly Date { get; set; }

    /// <summary>HICP index value, base 2015 = 100.</summary>
    public decimal Index { get; set; }
}
