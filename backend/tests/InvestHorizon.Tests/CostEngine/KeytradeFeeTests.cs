using FluentAssertions;
using InvestHorizon.Application.CostEngine;
using InvestHorizon.Domain.Enums;
using Xunit;

namespace InvestHorizon.Tests.CostEngine;

public class KeytradeFeeTests
{
    private readonly KeytradeFeeCalculator _calc = new();

    [Theory]
    [InlineData(100.0, 2.45)]        // ≤ €250
    [InlineData(250.0, 2.45)]        // boundary ≤ €250
    [InlineData(250.01, 5.95)]       // > €250 and ≤ €2,500
    [InlineData(2500.0, 5.95)]       // boundary ≤ €2,500
    [InlineData(2500.01, 14.95)]     // > €2,500 and ≤ €10,000
    [InlineData(10000.0, 14.95)]     // boundary ≤ €10,000
    [InlineData(10000.01, 22.45)]    // first extra €10k block: 14.95 + 7.50
    [InlineData(20000.0, 22.45)]     // still within first extra block
    [InlineData(20000.01, 29.95)]    // second extra block: 14.95 + 2 × 7.50
    public void Calculate_ReturnsEuronextBlockFee(decimal amount, decimal expected)
    {
        var fee = _calc.Calculate(amount, TransactionSide.Buy, InstrumentType.Share);
        fee.Should().BeApproximately(expected, 0.01m);
    }

    [Fact]
    public void Calculate_IsSymmetric_ForBuyAndSell()
    {
        var buy = _calc.Calculate(5_000m, TransactionSide.Buy, InstrumentType.Etf);
        var sell = _calc.Calculate(5_000m, TransactionSide.Sell, InstrumentType.Etf);
        buy.Should().Be(sell);
    }
}
