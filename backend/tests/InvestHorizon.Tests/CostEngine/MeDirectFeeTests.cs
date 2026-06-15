using FluentAssertions;
using InvestHorizon.Application.CostEngine;
using InvestHorizon.Domain.Enums;
using Xunit;

namespace InvestHorizon.Tests.CostEngine;

public class MeDirectFeeTests
{
    private readonly MeDirectFeeCalculator _calc = new();

    [Theory]
    [InlineData(1000.0)]
    [InlineData(50000.0)]
    public void Calculate_Etf_IsFree(decimal amount)
    {
        _calc.Calculate(amount, TransactionSide.Buy, InstrumentType.Etf).Should().Be(0m);
    }

    [Theory]
    [InlineData(1000.0, 7.0)]     // 0.15% = €1.50 → min €7 floor
    [InlineData(10000.0, 15.0)]   // 0.15% = €15.00
    public void Calculate_Share_PercentWithMin7(decimal amount, decimal expected)
    {
        _calc.Calculate(amount, TransactionSide.Buy, InstrumentType.Share)
            .Should().BeApproximately(expected, 0.01m);
    }

    [Theory]
    [InlineData(1000.0, 15.0)]    // 0.15% = €1.50 → min €15 floor
    [InlineData(20000.0, 30.0)]   // 0.15% = €30.00
    public void Calculate_Bond_PercentWithMin15(decimal amount, decimal expected)
    {
        _calc.Calculate(amount, TransactionSide.Buy, InstrumentType.Bond)
            .Should().BeApproximately(expected, 0.01m);
    }
}
