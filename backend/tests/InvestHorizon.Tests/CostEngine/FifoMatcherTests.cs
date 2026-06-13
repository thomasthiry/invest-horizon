using FluentAssertions;
using InvestHorizon.Application.CostEngine;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;
using Xunit;

namespace InvestHorizon.Tests.CostEngine;

public class FifoMatcherTests
{
    private readonly FifoMatcher _matcher = new();

    private static Transaction MakeBuy(decimal qty, decimal totalCost, DateOnly date) =>
        new()
        {
            Id = Guid.NewGuid(),
            Side = TransactionSide.Buy,
            Date = date,
            Quantity = qty,
            RemainingQuantity = qty,
            TotalCost = totalCost,
            NetProceeds = 0m
        };

    private static Transaction MakeSell(decimal qty, decimal netProceeds, DateOnly date) =>
        new()
        {
            Id = Guid.NewGuid(),
            Side = TransactionSide.Sell,
            Date = date,
            Quantity = qty,
            RemainingQuantity = 0m,
            NetProceeds = netProceeds
        };

    [Fact]
    public void Match_FullSell_SingleLot_ReturnsOneAllocation()
    {
        var buy = MakeBuy(10m, 1000m, new DateOnly(2024, 1, 1));
        var sell = MakeSell(10m, 1200m, new DateOnly(2024, 6, 1));

        var allocs = _matcher.Match(sell, [buy]);

        allocs.Should().HaveCount(1);
        allocs[0].Quantity.Should().Be(10m);
        allocs[0].RealizedGainEur.Should().BeApproximately(200m, 0.001m);
        buy.RemainingQuantity.Should().Be(0m);
    }

    [Fact]
    public void Match_PartialSell_ReducesBuyRemainingQuantity()
    {
        var buy = MakeBuy(33m, 5016.32m, new DateOnly(2024, 1, 1));
        var sell = MakeSell(10m, 500m, new DateOnly(2024, 6, 1));

        _matcher.Match(sell, [buy]);

        buy.RemainingQuantity.Should().BeApproximately(23m, 0.001m);
    }

    [Fact]
    public void Match_SpansTwoLots_FifoOrder()
    {
        var buy1 = MakeBuy(10m, 1000m, new DateOnly(2024, 1, 1));
        var buy2 = MakeBuy(10m, 1200m, new DateOnly(2024, 3, 1));
        var sell = MakeSell(15m, 1800m, new DateOnly(2024, 6, 1));

        var allocs = _matcher.Match(sell, [buy2, buy1]); // pass in wrong order; matcher should sort

        allocs.Should().HaveCount(2);
        // First alloc: 10 from buy1 (older)
        allocs[0].Quantity.Should().Be(10m);
        // Second alloc: 5 from buy2
        allocs[1].Quantity.Should().Be(5m);
        buy1.RemainingQuantity.Should().Be(0m);
        buy2.RemainingQuantity.Should().Be(5m);
    }

    [Fact]
    public void Match_InsufficientLots_Throws()
    {
        var buy = MakeBuy(5m, 500m, new DateOnly(2024, 1, 1));
        var sell = MakeSell(10m, 1000m, new DateOnly(2024, 6, 1));

        _matcher.Invoking(m => m.Match(sell, [buy]))
            .Should().Throw<InvalidOperationException>();
    }
}
