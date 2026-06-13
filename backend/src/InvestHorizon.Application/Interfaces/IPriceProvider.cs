using InvestHorizon.Domain.Entities;

namespace InvestHorizon.Application.Interfaces;

/// <summary>Quote returned by a price provider, in the instrument's native quote currency.</summary>
public record PriceQuote(string Symbol, decimal Price, string Currency, DateTime AsOf, string Source);

/// <summary>Abstraction over an external market-price source (Yahoo, etc.).</summary>
public interface IPriceProvider
{
    /// <summary>
    /// Fetch the latest quote for an instrument, resolving its symbol from ISIN/Ticker as needed.
    /// Returns <c>null</c> when no quote could be resolved (caller treats this as a soft failure).
    /// </summary>
    Task<PriceQuote?> GetQuoteAsync(Instrument instrument, CancellationToken ct = default);
}
