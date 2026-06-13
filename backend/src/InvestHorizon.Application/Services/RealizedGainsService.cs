using InvestHorizon.Application.Interfaces;

namespace InvestHorizon.Application.Services;

public sealed class RealizedGainsService
{
    private readonly ITransactionRepository _transactions;
    private readonly ICapitalGainsTaxService _taxService;

    public RealizedGainsService(ITransactionRepository transactions, ICapitalGainsTaxService taxService)
    {
        _transactions = transactions;
        _taxService = taxService;
    }

    public async Task<RealizedGainsReport> GetReportAsync(Guid portfolioId, int year, CancellationToken ct = default)
    {
        var allocations = await _transactions.GetAllocationsAsync(portfolioId, year, ct);

        var perSale = allocations
            .GroupBy(a => a.SellTransactionId)
            .Select(g => new SaleGainDto(
                SellTransactionId: g.Key,
                RealizedGainEur: g.Sum(a => a.RealizedGainEur)
            ))
            .ToList();

        var annualReport = _taxService.Compute(perSale.Select(s => s.RealizedGainEur), year);

        return new RealizedGainsReport(year, perSale, annualReport);
    }
}

public record SaleGainDto(Guid SellTransactionId, decimal RealizedGainEur);
public record RealizedGainsReport(int Year, IReadOnlyList<SaleGainDto> PerSale, AnnualTaxReport TaxReport);
