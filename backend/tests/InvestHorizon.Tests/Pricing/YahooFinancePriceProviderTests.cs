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
}
