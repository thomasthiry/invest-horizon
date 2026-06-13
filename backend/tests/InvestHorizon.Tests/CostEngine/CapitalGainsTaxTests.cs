using FluentAssertions;
using InvestHorizon.Application.CostEngine;
using Xunit;

namespace InvestHorizon.Tests.CostEngine;

public class CapitalGainsTaxTests
{
    [Fact]
    public void Compute_NetGainBelowExemption_ZeroTax()
    {
        var svc = new CapitalGainsTaxService(10_000m);
        var report = svc.Compute([5_000m], 2026);

        report.TaxDueEur.Should().Be(0m);
        report.TaxableBaseEur.Should().Be(0m);
    }

    [Fact]
    public void Compute_NetGainAboveExemption_TenPercentOnExcess()
    {
        var svc = new CapitalGainsTaxService(10_000m);
        var report = svc.Compute([15_000m], 2026);

        report.TaxableBaseEur.Should().Be(5_000m);
        report.TaxDueEur.Should().Be(500m);
    }

    [Fact]
    public void Compute_LossesOffsetGains_BeforeExemption()
    {
        var svc = new CapitalGainsTaxService(10_000m);
        // Gains: 20k, Losses: 5k → net 15k → taxable 5k → tax 500
        var report = svc.Compute([20_000m, -5_000m], 2026);

        report.GrossGainEur.Should().Be(20_000m);
        report.GrossLossEur.Should().Be(5_000m);
        report.NetGainEur.Should().Be(15_000m);
        report.TaxableBaseEur.Should().Be(5_000m);
        report.TaxDueEur.Should().Be(500m);
    }

    [Fact]
    public void Compute_NetLoss_ZeroTax()
    {
        var svc = new CapitalGainsTaxService(10_000m);
        var report = svc.Compute([1_000m, -5_000m], 2026);

        report.NetGainEur.Should().Be(-4_000m);
        report.TaxableBaseEur.Should().Be(0m);
        report.TaxDueEur.Should().Be(0m);
    }
}
