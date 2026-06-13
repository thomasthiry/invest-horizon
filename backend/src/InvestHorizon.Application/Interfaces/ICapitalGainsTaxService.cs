namespace InvestHorizon.Application.Interfaces;

public interface ICapitalGainsTaxService
{
    AnnualTaxReport Compute(IEnumerable<decimal> realizedGainsEur, int year);
}

public record AnnualTaxReport(
    int Year,
    decimal GrossGainEur,
    decimal GrossLossEur,
    decimal NetGainEur,
    decimal ExemptionEur,
    decimal TaxableBaseEur,
    decimal TaxDueEur
);
