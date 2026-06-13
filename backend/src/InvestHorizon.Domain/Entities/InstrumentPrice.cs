namespace InvestHorizon.Domain.Entities;

/// <summary>
/// Cached latest market price for an instrument (one row per instrument, upserted in place).
/// This is a cache of external data, not immutable domain history.
/// </summary>
public class InstrumentPrice
{
    public Guid InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>Price in the instrument's quote currency (see <see cref="Currency"/>).</summary>
    public decimal PriceNative { get; set; }

    /// <summary>Currency the quote is denominated in (e.g. "USD"); may differ from the instrument's nominal currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Market time of the quote as reported by the provider.</summary>
    public DateTime AsOf { get; set; }

    /// <summary>UTC timestamp of when we fetched this quote.</summary>
    public DateTime FetchedAt { get; set; }

    /// <summary>Provider that produced the quote, e.g. "Yahoo".</summary>
    public string Source { get; set; } = string.Empty;
}
