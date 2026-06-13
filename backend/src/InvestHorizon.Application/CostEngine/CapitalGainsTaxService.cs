using InvestHorizon.Application.Interfaces;

namespace InvestHorizon.Application.CostEngine;

public sealed class CapitalGainsTaxService : ICapitalGainsTaxService
{
    // Belgian annual exemption on capital gains (~€10,000; indexable — stored as config-overridable constant).
    public const decimal DefaultAnnualExemption = 10_000m;
    public const decimal TaxRate = 0.10m;

    private readonly decimal _annualExemption;

    public CapitalGainsTaxService(decimal annualExemption = DefaultAnnualExemption)
    {
        _annualExemption = annualExemption;
    }

    public AnnualTaxReport Compute(IEnumerable<decimal> realizedGainsEur, int year)
    {
        decimal gross = 0m, loss = 0m;
        foreach (var g in realizedGainsEur)
        {
            if (g >= 0) gross += g;
            else loss += Math.Abs(g);
        }

        var net = gross - loss;
        var taxable = Math.Max(0m, net - _annualExemption);
        var tax = Math.Round(taxable * TaxRate, 2);

        return new AnnualTaxReport(year, gross, loss, net, _annualExemption, taxable, tax);
    }
}
