namespace InvestHorizon.Domain.Entities;

/// <summary>
/// Cached daily closing price for an instrument (one row per instrument per trading day).
/// A cache of external time-series data used to reconstruct portfolio value over time,
/// not immutable domain history — rows are upserted when the cache is topped up.
/// </summary>
public class InstrumentPriceHistory
{
    public Guid InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>Trading day this close belongs to.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Closing price in the quote currency (see <see cref="Currency"/>).</summary>
    public decimal CloseNative { get; set; }

    /// <summary>Currency the close is denominated in (e.g. "USD").</summary>
    public string Currency { get; set; } = "EUR";
}
