using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.CostEngine;

public sealed class FifoMatcher : IFifoMatcher
{
    public IReadOnlyList<SaleAllocation> Match(Transaction sell, IList<Transaction> openBuyLots)
    {
        if (sell.Side != TransactionSide.Sell)
            throw new ArgumentException("Transaction must be a Sell.", nameof(sell));

        var allocations = new List<SaleAllocation>();
        var remaining = sell.Quantity;

        // Lots must be ordered oldest first (FIFO)
        foreach (var buy in openBuyLots.OrderBy(b => b.Date).ThenBy(b => b.Id))
        {
            if (remaining <= 0) break;
            if (buy.RemainingQuantity <= 0) continue;

            var allocated = Math.Min(remaining, buy.RemainingQuantity);

            // Proportional buy cost basis (EUR) for this quantity
            var buyFraction = allocated / buy.Quantity;
            var buyCostBasisEur = buy.TotalCost * buyFraction;

            // Proportional sell net proceeds (EUR) for this quantity
            var sellFraction = allocated / sell.Quantity;
            var sellProceedsEur = sell.NetProceeds * sellFraction;

            allocations.Add(new SaleAllocation
            {
                Id = Guid.NewGuid(),
                BuyTransactionId = buy.Id,
                SellTransactionId = sell.Id,
                Quantity = allocated,
                RealizedGainEur = sellProceedsEur - buyCostBasisEur,
                SaleYear = sell.Date.Year
            });

            buy.RemainingQuantity -= allocated;
            remaining -= allocated;
        }

        if (remaining > 0)
            throw new InvalidOperationException(
                $"Insufficient open buy quantity to cover sell of {sell.Quantity}. Remaining unmatched: {remaining}.");

        return allocations;
    }
}
