using FluentAssertions;
using InvestHorizon.Application.CostEngine;
using InvestHorizon.Domain.Enums;
using Xunit;

namespace InvestHorizon.Tests.CostEngine;

public class KeytradeFeeTests
{
    private readonly KeytradeFeeCalculator _calc = new();

    [Theory]
    [InlineData(139.09, 7.95)]       // ≤ €2,500
    [InlineData(2500.00, 7.95)]      // boundary ≤ €2,500
    [InlineData(4995.375, 14.95)]    // > €2,500 and ≤ €5,000
    [InlineData(2073.5, 7.95)]       // ≤ €2,500
    [InlineData(1449.0, 7.95)]       // ≤ €2,500
    [InlineData(10000.0, 19.95)]     // > €5,000 and ≤ €25,000
    [InlineData(30000.0, 24.60)]     // > €25,000: 30000 * 0.082% = 24.60
    public void Calculate_ReturnsTieredFee(decimal amount, decimal expected)
    {
        var fee = _calc.Calculate(amount, TransactionSide.Buy);
        fee.Should().BeApproximately(expected, 0.01m);
    }

    [Fact]
    public void Calculate_AboveMax_MinFeeApplies()
    {
        // Very small amount above €25k: 0.082% might be < €19.95
        var fee = _calc.Calculate(25_001m, TransactionSide.Buy);
        fee.Should().BeGreaterThanOrEqualTo(19.95m);
    }
}
