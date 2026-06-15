using FluentAssertions;
using InvestHorizon.Application.CostEngine;
using InvestHorizon.Domain.Enums;
using Xunit;

namespace InvestHorizon.Tests.CostEngine;

public class RevolutFeeTests
{
    private readonly RevolutFeeCalculator _calc = new();

    [Theory]
    [InlineData(100.0, 1.0)]      // 0.25% = €0.25 → min €1 floor applies
    [InlineData(400.0, 1.0)]      // 0.25% = €1.00 → at the floor
    [InlineData(1000.0, 2.50)]    // 0.25% = €2.50
    [InlineData(10000.0, 25.0)]   // 0.25% = €25.00
    public void Calculate_ReturnsPercentWithMinimum(decimal amount, decimal expected)
    {
        var fee = _calc.Calculate(amount, TransactionSide.Buy, InstrumentType.Share);
        fee.Should().BeApproximately(expected, 0.01m);
    }
}
