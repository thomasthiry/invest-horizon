using InvestHorizon.Domain.Entities;

namespace InvestHorizon.Application.Interfaces;

/// <summary>Quote returned by a price provider, in the instrument's native quote currency.</summary>
public record PriceQuote(string Symbol, decimal Price, string Currency, DateTime AsOf, string Source);

/// <summary>A single daily close in the instrument's native quote currency.</summary>
public record PriceHistoryPoint(DateOnly Date, decimal CloseNative, string Currency);

/// <summary>Abstraction over an external market-price source (Yahoo, etc.).</summary>
public interface IPriceProvider
{
    /// <summary>
    /// Fetch the latest quote for an instrument, resolving its symbol from ISIN/Ticker as needed.
    /// Returns <c>null</c> when no quote could be resolved (caller treats this as a soft failure).
    /// </summary>
    Task<PriceQuote?> GetQuoteAsync(Instrument instrument, CancellationToken ct = default);

    /// <summary>
    /// Fetch daily closing prices for an instrument over the inclusive <paramref name="from"/>..<paramref name="to"/>
    /// range, resolving its symbol as needed. Returns an empty list when nothing could be resolved
    /// (caller treats this as a soft failure). Only trading days are returned.
    /// </summary>
    Task<IReadOnlyList<PriceHistoryPoint>> GetHistoryAsync(
        Instrument instrument, DateOnly from, DateOnly to, CancellationToken ct = default);
}
