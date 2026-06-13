using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestHorizon.Infrastructure.Persistence.Repositories;

public sealed class PortfolioRepository : IPortfolioRepository
{
    private readonly AppDbContext _db;
    public PortfolioRepository(AppDbContext db) => _db = db;

    public async Task<Portfolio?> GetByIdAsync(Guid id, string userId, CancellationToken ct = default)
        => await _db.Portfolios.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

    public async Task<IReadOnlyList<Portfolio>> GetByUserAsync(string userId, CancellationToken ct = default)
        => await _db.Portfolios.Where(p => p.UserId == userId).ToListAsync(ct);

    public async Task AddAsync(Portfolio portfolio, CancellationToken ct = default)
        => await _db.Portfolios.AddAsync(portfolio, ct);

    public Task UpdateAsync(Portfolio portfolio, CancellationToken ct = default)
    {
        _db.Portfolios.Update(portfolio);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
