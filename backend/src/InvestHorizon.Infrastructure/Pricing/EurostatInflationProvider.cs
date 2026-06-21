using System.Text.Json;
using InvestHorizon.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvestHorizon.Infrastructure.Pricing;

/// <summary>
/// Fetches Belgian HICP (all-items, base 2015=100) from the free, keyless Eurostat dissemination API.
/// Returns all available months in a single call; the caller is responsible for caching.
/// </summary>
public sealed class EurostatInflationProvider : IInflationProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<EurostatInflationProvider> _logger;

    public EurostatInflationProvider(HttpClient http, ILogger<EurostatInflationProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<DateOnly, decimal>> GetIndexHistoryAsync(
        string region, CancellationToken ct = default)
    {
        var url = "https://ec.europa.eu/eurostat/api/dissemination/statistics/1.0/data/prc_hicp_midx" +
                  $"?format=JSON&geo={region}&coicop=CP00&unit=I15";
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Eurostat HICP returned {Status} for {Region}", resp.StatusCode, region);
                return new Dictionary<DateOnly, decimal>();
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            return ParseResponse(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch HICP from Eurostat for {Region}", region);
            return new Dictionary<DateOnly, decimal>();
        }
    }

    /// <summary>
    /// Parses a Eurostat JSON-stat 2.0 response for a single-region HICP dataset.
    /// The "value" field may be a sparse object (string int keys) or a dense array;
    /// both representations are handled. Exposed for testing.
    /// </summary>
    public static IReadOnlyDictionary<DateOnly, decimal> ParseResponse(string json)
    {
        var result = new Dictionary<DateOnly, decimal>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Build position → DateOnly from the time dimension's category index.
        if (!root.TryGetProperty("dimension", out var dim) ||
            !dim.TryGetProperty("time", out var timeDim) ||
            !timeDim.TryGetProperty("category", out var cat) ||
            !cat.TryGetProperty("index", out var timeIndex))
            return result;

        var posToDate = new Dictionary<int, DateOnly>();
        foreach (var prop in timeIndex.EnumerateObject())
        {
            // Keys are "YYYY-MM".
            if (prop.Name.Length >= 7 &&
                int.TryParse(prop.Name[..4], out var year) &&
                int.TryParse(prop.Name.AsSpan(5, 2), out var month) &&
                prop.Value.ValueKind == JsonValueKind.Number)
            {
                posToDate[prop.Value.GetInt32()] = new DateOnly(year, month, 1);
            }
        }

        if (!root.TryGetProperty("value", out var valProp)) return result;

        if (valProp.ValueKind == JsonValueKind.Array)
        {
            // Dense array: position == array index.
            var i = 0;
            foreach (var v in valProp.EnumerateArray())
            {
                if (v.ValueKind == JsonValueKind.Number && posToDate.TryGetValue(i, out var date))
                    result[date] = v.GetDecimal();
                i++;
            }
        }
        else if (valProp.ValueKind == JsonValueKind.Object)
        {
            // Sparse object: keys are string integers.
            foreach (var v in valProp.EnumerateObject())
            {
                if (int.TryParse(v.Name, out var pos) &&
                    v.Value.ValueKind == JsonValueKind.Number &&
                    posToDate.TryGetValue(pos, out var date))
                {
                    result[date] = v.Value.GetDecimal();
                }
            }
        }

        return result;
    }
}
