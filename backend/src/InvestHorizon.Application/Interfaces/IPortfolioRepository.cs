using InvestHorizon.Domain.Entities;

namespace InvestHorizon.Application.Interfaces;

public interface IPortfolioRepository
{
    Task<Portfolio?> GetByIdAsync(Guid id, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<Portfolio>> GetByUserAsync(string userId, CancellationToken ct = default);
    Task AddAsync(Portfolio portfolio, CancellationToken ct = default);
    Task UpdateAsync(Portfolio portfolio, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
