using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByPortfolioAsync(Guid portfolioId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetOpenBuyLotsAsync(Guid portfolioId, Guid instrumentId, CancellationToken ct = default);
    Task<IReadOnlyList<SaleAllocation>> GetAllocationsAsync(Guid portfolioId, int? year, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByPortfolioAndInstrumentAsync(Guid portfolioId, Guid instrumentId, CancellationToken ct = default);
    Task RemoveAllocationsForSellsAsync(IEnumerable<Guid> sellTransactionIds, CancellationToken ct = default);
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    Task AddAllocationsAsync(IEnumerable<SaleAllocation> allocations, CancellationToken ct = default);
    Task UpdateAsync(Transaction transaction, CancellationToken ct = default);
    Task DeleteAsync(Transaction transaction, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
