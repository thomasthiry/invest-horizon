using FluentAssertions;
using InvestHorizon.Infrastructure.Pricing;
using Xunit;

namespace InvestHorizon.Tests.Pricing;

public class FrankfurterFxRateProviderTests
{
    private const string TimeSeriesJson = """
    {
      "amount": 1.0,
      "base": "EUR",
      "start_date": "2024-01-02",
      "end_date": "2024-01-04",
      "rates": {
        "2024-01-02": { "USD": 1.09 },
        "2024-01-03": { "USD": 1.092 },
        "2024-01-04": { "USD": 1.095 }
      }
    }
    """;

    [Fact]
    public void ParseTimeSeries_MapsDatesToRates()
    {
        var rates = FrankfurterFxRateProvider.ParseTimeSeries(TimeSeriesJson, "USD");

        rates.Should().HaveCount(3);
        rates[new DateOnly(2024, 1, 2)].Should().Be(1.09m);
        rates[new DateOnly(2024, 1, 4)].Should().Be(1.095m);
    }

    [Fact]
    public void ParseTimeSeries_ReturnsEmpty_WhenNoRates()
    {
        var rates = FrankfurterFxRateProvider.ParseTimeSeries("""{"base":"EUR"}""", "USD");
        rates.Should().BeEmpty();
    }

    [Fact]
    public void ParseTimeSeries_IgnoresMissingCurrency()
    {
        var rates = FrankfurterFxRateProvider.ParseTimeSeries(TimeSeriesJson, "GBP");
        rates.Should().BeEmpty();
    }
}
