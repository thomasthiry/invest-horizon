using FluentAssertions;
using InvestHorizon.Infrastructure.Pricing;
using Xunit;

namespace InvestHorizon.Tests.Pricing;

public class YahooFinancePriceProviderTests
{
    private const string ChartJson = """
    {
      "chart": {
        "result": [
          {
            "meta": {
              "currency": "USD",
              "symbol": "AAPL",
              "regularMarketPrice": 212.34,
              "regularMarketTime": 1718304000
            }
          }
        ],
        "error": null
      }
    }
    """;

    [Fact]
    public void ParseChart_ReadsPriceCurrencyAndTime()
    {
        var quote = YahooFinancePriceProvider.ParseChart(ChartJson, "AAPL");

        quote.Should().NotBeNull();
        quote!.Price.Should().Be(212.34m);
        quote.Currency.Should().Be("USD");
        quote.Source.Should().Be("Yahoo");
        quote.AsOf.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1718304000).UtcDateTime);
    }

    [Fact]
    public void ParseChart_NormalisesGbpPenceToGbp()
    {
        var json = ChartJson.Replace("\"USD\"", "\"GBp\"").Replace("212.34", "10500");

        var quote = YahooFinancePriceProvider.ParseChart(json, "VWRL.L");

        quote.Should().NotBeNull();
        quote!.Currency.Should().Be("GBP");
        quote.Price.Should().Be(105m); // 10500 pence -> 105 GBP
    }

    [Fact]
    public void ParseChart_ReturnsNull_WhenNoResult()
    {
        var quote = YahooFinancePriceProvider.ParseChart("""{"chart":{"result":[],"error":null}}""", "XXXX");
        quote.Should().BeNull();
    }

    [Fact]
    public void ParseChart_ReturnsNull_WhenNoPrice()
    {
        var json = """{"chart":{"result":[{"meta":{"currency":"EUR"}}]}}""";
        var quote = YahooFinancePriceProvider.ParseChart(json, "XXXX");
        quote.Should().BeNull();
    }

    private const string HistoryJson = """
    {
      "chart": {
        "result": [
          {
            "meta": { "currency": "USD", "symbol": "AAPL" },
            "timestamp": [ 1718236800, 1718323200, 1718409600 ],
            "indicators": {
              "quote": [ { "close": [ 210.5, null, 212.0 ] } ]
            }
          }
        ],
        "error": null
      }
    }
    """;

    [Fact]
    public void ParseChartHistory_ZipsTimestampsAndCloses_SkippingNulls()
    {
        var points = YahooFinancePriceProvider.ParseChartHistory(HistoryJson, "AAPL");

        points.Should().HaveCount(2); // the null close is skipped
        points[0].Date.Should().Be(DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(1718236800).UtcDateTime));
        points[0].CloseNative.Should().Be(210.5m);
        points[0].Currency.Should().Be("USD");
        points[1].CloseNative.Should().Be(212.0m);
    }

    [Fact]
    public void ParseChartHistory_NormalisesGbpPenceToGbp()
    {
        var json = HistoryJson.Replace("\"USD\"", "\"GBp\"").Replace("210.5", "10500");

        var points = YahooFinancePriceProvider.ParseChartHistory(json, "VWRL.L");

        points.Should().HaveCount(2);
        points[0].Currency.Should().Be("GBP");
        points[0].CloseNative.Should().Be(105m);
    }

    [Fact]
    public void ParseChartHistory_ReturnsEmpty_WhenNoResult()
    {
        var points = YahooFinancePriceProvider.ParseChartHistory("""{"chart":{"result":[],"error":null}}""", "XXXX");
        points.Should().BeEmpty();
    }
}
