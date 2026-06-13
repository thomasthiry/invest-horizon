using System.Net.Http.Json;
using System.Text.Json;
using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InvestHorizon.Infrastructure.Pricing;

/// <summary>
/// Price provider backed by Yahoo Finance's public (unofficial) endpoints. No API key required.
/// Resolves an ISIN to a Yahoo symbol via the search endpoint, then reads the latest price
/// from the chart endpoint (which does not require a crumb/cookie).
/// </summary>
public sealed class YahooFinancePriceProvider : IPriceProvider
{
    public const string SourceName = "Yahoo";
    public const string HttpClientName = "Yahoo";

    private readonly HttpClient _http;
    private readonly ILogger<YahooFinancePriceProvider> _logger;

    public YahooFinancePriceProvider(HttpClient http, ILogger<YahooFinancePriceProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<PriceQuote?> GetQuoteAsync(Instrument instrument, CancellationToken ct = default)
    {
        // Preferred order: cached Yahoo symbol → bare ticker (US names only, no exchange suffix) → ISIN search.
        // If PriceSymbol is cached it was already confirmed working, so trust it.
        if (!string.IsNullOrWhiteSpace(instrument.PriceSymbol))
            return await FetchQuoteAsync(instrument.PriceSymbol, ct);

        // Try Ticker first (fast, no search), but fall back to ISIN search on failure — tickers stored
        // without an exchange suffix (e.g. "EVS" instead of "EVS.BR") return no price for non-US listings.
        if (!string.IsNullOrWhiteSpace(instrument.Ticker))
        {
            var q = await FetchQuoteAsync(instrument.Ticker, ct);
            if (q is not null) return q;
            _logger.LogDebug("Ticker {Ticker} gave no price, falling back to ISIN search for {Isin}",
                instrument.Ticker, instrument.Isin);
        }

        var symbol = await ResolveSymbolAsync(instrument.Isin, ct);
        if (string.IsNullOrWhiteSpace(symbol)) return null;

        return await FetchQuoteAsync(symbol, ct);
    }

    private async Task<string?> ResolveSymbolAsync(string isin, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(
            $"https://query1.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(isin)}", ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Yahoo search for {Isin} returned {Status}", isin, resp.StatusCode);
            return null;
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("quotes", out var quotes) &&
            quotes.ValueKind == JsonValueKind.Array && quotes.GetArrayLength() > 0 &&
            quotes[0].TryGetProperty("symbol", out var sym))
        {
            return sym.GetString();
        }

        _logger.LogWarning("Yahoo search for {Isin} returned no symbol", isin);
        return null;
    }

    private async Task<PriceQuote?> FetchQuoteAsync(string symbol, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(
            $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}", ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Yahoo chart for {Symbol} returned {Status}", symbol, resp.StatusCode);
            return null;
        }

        return ParseChart(await resp.Content.ReadAsStringAsync(ct), symbol, _logger);
    }

    /// <summary>Parses the Yahoo chart response. Exposed for unit testing.</summary>
    public static PriceQuote? ParseChart(string json, string symbol, ILogger? logger = null)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("chart", out var chart) ||
            !chart.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
        {
            logger?.LogWarning("Yahoo chart for {Symbol} had no result", symbol);
            return null;
        }

        var meta = result[0].GetProperty("meta");
        if (!meta.TryGetProperty("regularMarketPrice", out var priceEl) ||
            priceEl.ValueKind != JsonValueKind.Number)
        {
            logger?.LogWarning("Yahoo chart for {Symbol} had no regularMarketPrice", symbol);
            return null;
        }

        var price = priceEl.GetDecimal();
        var currency = meta.TryGetProperty("currency", out var ccyEl) ? ccyEl.GetString() ?? "EUR" : "EUR";

        // Yahoo quotes UK instruments in pence ("GBp"); normalise to GBP so FX conversion works.
        if (string.Equals(currency, "GBp", StringComparison.Ordinal))
        {
            price /= 100m;
            currency = "GBP";
        }

        var asOf = meta.TryGetProperty("regularMarketTime", out var timeEl) && timeEl.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(timeEl.GetInt64()).UtcDateTime
            : DateTime.UtcNow;

        return new PriceQuote(symbol, price, currency, asOf, SourceName);
    }
}
