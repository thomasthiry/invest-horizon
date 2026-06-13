using InvestHorizon.Domain.Entities;

namespace InvestHorizon.Application.Interfaces;

public interface IFifoMatcher
{
    /// <summary>
    /// Matches a sell transaction against open buy lots (FIFO) within the same portfolio+instrument.
    /// Returns the SaleAllocation records produced; also mutates RemainingQuantity on buy lots.
    /// </summary>
    IReadOnlyList<SaleAllocation> Match(Transaction sell, IList<Transaction> openBuyLots);
}
