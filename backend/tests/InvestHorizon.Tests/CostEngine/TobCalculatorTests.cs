using FluentAssertions;
using InvestHorizon.Application.CostEngine;
using InvestHorizon.Domain.Enums;
using Xunit;

namespace InvestHorizon.Tests.CostEngine;

public class TobCalculatorTests
{
    private readonly BelgianTobCalculator _calc = new();

    [Theory]
    [InlineData(139.09, InstrumentType.Etf, 0.17)]     // Excel row 2: 139.09 * 0.12% = 0.167 ≈ 0.17
    [InlineData(4995.375, InstrumentType.Etf, 5.99)]   // Excel row 3: 4995.375 * 0.12% = 5.994
    [InlineData(2073.5, InstrumentType.Share, 7.26)]   // Excel row 4: 2073.5 * 0.35% = 7.257
    [InlineData(1449.0, InstrumentType.Share, 5.07)]   // Excel row 5: 1449.0 * 0.35% = 5.0715
    public void Calculate_ReturnCorrectTob(decimal amountEur, InstrumentType type, decimal expected)
    {
        var result = _calc.Calculate(amountEur, type);
        result.Should().BeApproximately(expected, 0.01m);
    }

    [Fact]
    public void Calculate_Share_CapsAt1600()
    {
        var tob = _calc.Calculate(1_000_000m, InstrumentType.Share);
        tob.Should().Be(1_600m);
    }

    [Fact]
    public void Calculate_Etf_CapsAt1300()
    {
        // 0.12% cap triggers above €1,083,334 (1,300 / 0.0012)
        var tob = _calc.Calculate(2_000_000m, InstrumentType.Etf);
        tob.Should().Be(1_300m);
    }

    [Fact]
    public void Calculate_CapitalizingFund_CapsAt4000()
    {
        var tob = _calc.Calculate(1_000_000m, InstrumentType.CapitalizingFund);
        tob.Should().Be(4_000m);
    }

    [Fact]
    public void Calculate_IsSymmetric_SameBuyAndSell()
    {
        // TOB applies the same rate whether buying or selling
        var buy = _calc.Calculate(5_000m, InstrumentType.Share);
        var sell = _calc.Calculate(5_000m, InstrumentType.Share);
        buy.Should().Be(sell);
    }
}
