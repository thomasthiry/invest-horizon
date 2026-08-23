using FluentAssertions;
using InvestHorizon.Application.CostEngine;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;
using Xunit;

namespace InvestHorizon.Tests.CostEngine;

public class ExitCostEstimatorTests
{
    private static ExitCostEstimator Estimator() => new(
        [new KeytradeFeeCalculator(), new RevolutFeeCalculator(), new MeDirectFeeCalculator()],
        new BelgianTobCalculator());

    private static Transaction Lot(Broker broker, decimal remaining, decimal? quantity = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Side = TransactionSide.Buy,
            Broker = broker,
            Quantity = quantity ?? remaining,
            RemainingQuantity = remaining,
        };

    [Fact]
    public void Estimate_ChargesOneOrderPerBroker_NotOnePerLot()
    {
        // Three Keytrade lots totalling 100 shares @ EUR 40 = EUR 4,000 sold in ONE order.
        // Keytrade grid: 4,000 is in the <= 10,000 block -> 14.95 (not 3 x 5.95).
        // TOB (share) 0.35% of 4,000 = 14.00.
        var lots = new[] { Lot(Broker.Keytrade, 30m), Lot(Broker.Keytrade, 50m), Lot(Broker.Keytrade, 20m) };

        var estimate = Estimator().Estimate(lots, 40m, InstrumentType.Share);

        estimate.TotalEur.Should().Be(14.95m + 14.00m);
        estimate.Orders.Should().ContainSingle();
        var order = estimate.Orders[0];
        order.Broker.Should().Be(Broker.Keytrade);
        order.Quantity.Should().Be(100m);          // the three lots merged into one order
        order.UnitPriceEur.Should().Be(40m);
        order.OrderValueEur.Should().Be(4_000m);
        order.BrokerFeeEur.Should().Be(14.95m);
        order.TobEur.Should().Be(14.00m);
        order.TotalEur.Should().Be(28.95m);
    }

    [Fact]
    public void Estimate_SumsFeesOfBothBrokers_WhenPositionIsSplit()
    {
        // 100 shares @ EUR 40: 60 at Keytrade (2,400) + 40 at Revolut (1,600).
        // Keytrade: 2,400 <= 2,500 -> 5.95;  TOB 0.35% of 2,400 = 8.40
        // Revolut:  0.25% of 1,600 = 4.00;   TOB 0.35% of 1,600 = 5.60
        var lots = new[] { Lot(Broker.Keytrade, 60m), Lot(Broker.Revolut, 40m) };

        var estimate = Estimator().Estimate(lots, 40m, InstrumentType.Share);

        estimate.TotalEur.Should().Be(5.95m + 8.40m + 4.00m + 5.60m);
        estimate.Orders.Should().HaveCount(2);

        var keytrade = estimate.Orders.Single(o => o.Broker == Broker.Keytrade);
        keytrade.Quantity.Should().Be(60m);
        keytrade.OrderValueEur.Should().Be(2_400m);
        keytrade.BrokerFeeEur.Should().Be(5.95m);
        keytrade.TobEur.Should().Be(8.40m);

        var revolut = estimate.Orders.Single(o => o.Broker == Broker.Revolut);
        revolut.Quantity.Should().Be(40m);
        revolut.OrderValueEur.Should().Be(1_600m);
        revolut.BrokerFeeEur.Should().Be(4.00m);
        revolut.TobEur.Should().Be(5.60m);
    }

    [Fact]
    public void Estimate_ReturnsOrdersLargestFirst()
    {
        // The UI shows these in order, so the biggest order must come first regardless of lot order.
        var lots = new[] { Lot(Broker.Revolut, 10m), Lot(Broker.Keytrade, 90m) };

        var estimate = Estimator().Estimate(lots, 40m, InstrumentType.Share);

        estimate.Orders.Select(o => o.Broker).Should().Equal(Broker.Keytrade, Broker.Revolut);
    }

    [Fact]
    public void Estimate_OrderValuesSumToThePositionValue()
    {
        // The breakdown must reconcile with the market value shown next to it.
        var lots = new[] { Lot(Broker.Keytrade, 60m), Lot(Broker.Revolut, 40m) };

        var estimate = Estimator().Estimate(lots, 40m, InstrumentType.Share);

        estimate.Orders.Sum(o => o.OrderValueEur).Should().Be(100m * 40m);
        estimate.Orders.Sum(o => o.TotalEur).Should().Be(estimate.TotalEur);
    }

    [Fact]
    public void Estimate_ChargesEachBrokersMinimumFeeSeparately()
    {
        // 100 shares @ EUR 10 = EUR 1,000, half at Revolut (500) and half at MeDirect (500).
        //   Revolut  0.25% of 500 = 1.25 (above the 1.00 min)  ·  TOB 0.35% of 500 = 1.75
        //   MeDirect 0.15% of 500 = 0.75 -> lifted to 7.00     ·  TOB 0.35% of 500 = 1.75
        var split = Estimator().Estimate(
            [Lot(Broker.Revolut, 50m), Lot(Broker.MeDirect, 50m)], 10m, InstrumentType.Share);

        split.TotalEur.Should().Be(1.25m + 1.75m + 7.00m + 1.75m);
        split.Orders.Single(o => o.Broker == Broker.MeDirect).BrokerFeeEur.Should().Be(7.00m);

        // The same EUR 1,000 held entirely at Revolut is one order, so one fee.
        var single = Estimator().Estimate([Lot(Broker.Revolut, 100m)], 10m, InstrumentType.Share);
        single.TotalEur.Should().Be(2.50m + 3.50m);
    }

    [Fact]
    public void Estimate_AppliesTobCapPerOrder_SoASplitPositionCanPayTwoCaps()
    {
        // Capitalizing fund: TOB 1.32% capped at EUR 4,000 per order.
        // 400,000 at one broker -> 1.32% = 5,280 -> capped to 4,000 (one cap).
        // Split 200,000 / 200,000 -> 1.32% = 2,640 each -> under the cap, 5,280 of TOB in total.
        var lotsSingle = new[] { Lot(Broker.Revolut, 400_000m) };
        var lotsSplit = new[] { Lot(Broker.Revolut, 200_000m), Lot(Broker.Keytrade, 200_000m) };

        var single = Estimator().Estimate(lotsSingle, 1m, InstrumentType.CapitalizingFund);
        var split = Estimator().Estimate(lotsSplit, 1m, InstrumentType.CapitalizingFund);

        single.TotalEur.Should().Be(4_000m + 1_000m);   // TOB capped + Revolut 0.25% of 400k
        single.Orders[0].TobEur.Should().Be(4_000m);    // the cap, visible in the breakdown

        // TOB uncapped on both legs + both broker fees.
        split.TotalEur.Should().Be(2_640m + 500m + 2_640m + 14.95m + 7.50m * 19);
        split.Orders.Should().OnlyContain(o => o.TobEur == 2_640m);
    }

    [Fact]
    public void Estimate_UsesSellSideFeeGrid_PerInstrumentType()
    {
        // MeDirect: ETFs are free, shares are 0.15% with a EUR 7 minimum.
        var lots = new[] { Lot(Broker.MeDirect, 100m) };

        var etf = Estimator().Estimate(lots, 40m, InstrumentType.Etf);
        var share = Estimator().Estimate(lots, 40m, InstrumentType.Share);

        etf.TotalEur.Should().Be(0m + 4.80m);        // no broker fee + TOB 0.12% of 4,000
        etf.Orders[0].BrokerFeeEur.Should().Be(0m);
        share.TotalEur.Should().Be(7.00m + 14.00m);  // 0.15% of 4,000 = 6.00 -> the 7.00 min, + TOB 0.35%
    }

    [Fact]
    public void Estimate_IgnoresFullyConsumedLots()
    {
        // A lot with nothing left must not drag a broker's order into the estimate.
        var lots = new[] { Lot(Broker.Keytrade, 100m), Lot(Broker.Revolut, 0m, quantity: 50m) };

        var estimate = Estimator().Estimate(lots, 40m, InstrumentType.Share);

        estimate.TotalEur.Should().Be(14.95m + 14.00m); // Keytrade only
        estimate.Orders.Should().ContainSingle().Which.Broker.Should().Be(Broker.Keytrade);
    }

    [Fact]
    public void Estimate_IsZero_WhenThereIsNothingToSell()
    {
        var empty = Estimator().Estimate([], 40m, InstrumentType.Share);
        empty.TotalEur.Should().Be(0m);
        empty.Orders.Should().BeEmpty();

        var consumed = Estimator().Estimate([Lot(Broker.Keytrade, 0m, quantity: 10m)], 40m, InstrumentType.Share);
        consumed.TotalEur.Should().Be(0m);
        consumed.Orders.Should().BeEmpty();
    }

    [Fact]
    public void Estimate_IsZero_WhenThePositionIsWorthless()
    {
        // No order value -> no fee and no tax; charging a minimum fee here would be fiction.
        var estimate = Estimator().Estimate([Lot(Broker.Revolut, 100m)], 0m, InstrumentType.Share);

        estimate.TotalEur.Should().Be(0m);
        estimate.Orders.Should().BeEmpty();
    }

    [Fact]
    public void Estimate_Throws_WhenNoCalculatorIsRegisteredForABroker()
    {
        var partial = new ExitCostEstimator([new KeytradeFeeCalculator()], new BelgianTobCalculator());

        var act = () => partial.Estimate([Lot(Broker.Revolut, 10m)], 40m, InstrumentType.Share);

        act.Should().Throw<NotSupportedException>().WithMessage("*Revolut*");
    }
}
