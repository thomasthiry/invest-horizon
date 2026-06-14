using System.Text.Json;
using InvestHorizon.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace InvestHorizon.Infrastructure.Pricing;

/// <summary>
/// FX rates from the free, keyless Frankfurter / ECB API (https://frankfurter.dev), EUR-based.
/// Rates are published once per business day, so they are cached in memory until the next UTC day.
/// </summary>
public sealed class FrankfurterFxRateProvider : IFxRateProvider
{
    public const string HttpClientName = "Frankfurter";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FrankfurterFxRateProvider> _logger;

    public FrankfurterFxRateProvider(HttpClient http, IMemoryCache cache, ILogger<FrankfurterFxRateProvider> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<decimal> GetEurRateAsync(string currency, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currency) ||
            string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase))
            return 1m;

        var ccy = currency.ToUpperInvariant();
        var cacheKey = $"fx:{DateTime.UtcNow:yyyy-MM-dd}:{ccy}";
        if (_cache.TryGetValue(cacheKey, out decimal cached))
            return cached;

        try
        {
            using var resp = await _http.GetAsync(
                $"https://api.frankfurter.dev/v1/latest?base=EUR&symbols={ccy}", ct);
            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("rates", out var rates) &&
                    rates.TryGetProperty(ccy, out var rateEl) && rateEl.ValueKind == JsonValueKind.Number)
                {
                    var rate = rateEl.GetDecimal();
                    _cache.Set(cacheKey, rate, TimeSpan.FromHours(12));
                    return rate;
                }
            }
            _logger.LogWarning("Frankfurter returned no rate for {Currency} ({Status})", ccy, resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch FX rate for {Currency}", ccy);
        }

        // Signal "unknown" with 0 so the caller leaves market value null rather than mis-converting.
        return 0m;
    }

    public async Task<IReadOnlyDictionary<DateOnly, decimal>> GetEurRateHistoryAsync(
        string currency, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currency) ||
            string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase))
            return new Dictionary<DateOnly, decimal>();

        var ccy = currency.ToUpperInvariant();
        try
        {
            using var resp = await _http.GetAsync(
                $"https://api.frankfurter.dev/v1/{from:yyyy-MM-dd}..{to:yyyy-MM-dd}?base=EUR&symbols={ccy}", ct);
            if (resp.IsSuccessStatusCode)
                return ParseTimeSeries(await resp.Content.ReadAsStringAsync(ct), ccy);

            _logger.LogWarning("Frankfurter history for {Currency} returned {Status}", ccy, resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch FX history for {Currency}", ccy);
        }

        return new Dictionary<DateOnly, decimal>();
    }

    /// <summary>Parses a Frankfurter time-series response ({ rates: { "yyyy-MM-dd": { CCY: rate } } }). Exposed for testing.</summary>
    public static IReadOnlyDictionary<DateOnly, decimal> ParseTimeSeries(string json, string currency)
    {
        var result = new Dictionary<DateOnly, decimal>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("rates", out var rates) || rates.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var day in rates.EnumerateObject())
        {
            if (!DateOnly.TryParse(day.Name, out var date)) continue;
            if (day.Value.TryGetProperty(currency, out var rateEl) && rateEl.ValueKind == JsonValueKind.Number)
                result[date] = rateEl.GetDecimal();
        }

        return result;
    }
}
