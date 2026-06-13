using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.Services;

public sealed class HoldingsService
{
    private readonly ITransactionRepository _transactions;
    private readonly IInstrumentRepository _instruments;

    public HoldingsService(ITransactionRepository transactions, IInstrumentRepository instruments)
    {
        _transactions = transactions;
        _instruments = instruments;
    }

    public async Task<IReadOnlyList<HoldingDto>> GetHoldingsAsync(Guid portfolioId, CancellationToken ct = default)
    {
        var txs = await _transactions.GetByPortfolioAsync(portfolioId, ct);
        var buyLots = txs.Where(t => t.Side == TransactionSide.Buy && t.RemainingQuantity > 0).ToList();

        var holdings = buyLots
            .GroupBy(t => t.InstrumentId)
            .Select(g =>
            {
                var totalQty = g.Sum(t => t.RemainingQuantity);
                var totalCostEur = g.Sum(t => t.TotalCost * (t.RemainingQuantity / t.Quantity));
                var avgCostEur = totalQty > 0 ? totalCostEur / totalQty : 0m;

                var first = g.First();
                return new HoldingDto(
                    InstrumentId: g.Key,
                    Isin: first.Instrument?.Isin ?? string.Empty,
                    Name: first.Instrument?.Name ?? string.Empty,
                    Currency: first.Currency,
                    OpenQuantity: totalQty,
                    AvgCostEur: avgCostEur,
                    TotalInvestedEur: totalCostEur
                );
            })
            .Where(h => h.OpenQuantity > 0)
            .OrderBy(h => h.Name)
            .ToList();

        return holdings;
    }
}

public record HoldingDto(
    Guid InstrumentId,
    string Isin,
    string Name,
    string Currency,
    decimal OpenQuantity,
    decimal AvgCostEur,
    decimal TotalInvestedEur
);
