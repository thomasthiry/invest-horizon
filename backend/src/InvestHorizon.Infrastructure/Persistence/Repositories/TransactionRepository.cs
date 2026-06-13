using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvestHorizon.Infrastructure.Persistence.Repositories;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _db;
    public TransactionRepository(AppDbContext db) => _db = db;

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Transactions.Include(t => t.Instrument).FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Transaction>> GetByPortfolioAsync(Guid portfolioId, CancellationToken ct = default)
        => await _db.Transactions
            .Include(t => t.Instrument)
            .Where(t => t.PortfolioId == portfolioId)
            .OrderBy(t => t.Date)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Transaction>> GetOpenBuyLotsAsync(Guid portfolioId, Guid instrumentId, CancellationToken ct = default)
        => await _db.Transactions
            .Where(t => t.PortfolioId == portfolioId
                     && t.InstrumentId == instrumentId
                     && t.Side == TransactionSide.Buy
                     && t.RemainingQuantity > 0)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SaleAllocation>> GetAllocationsAsync(Guid portfolioId, int? year, CancellationToken ct = default)
    {
        var query = _db.SaleAllocations
            .Where(a => a.SellTransaction.PortfolioId == portfolioId);
        if (year.HasValue)
            query = query.Where(a => a.SaleYear == year.Value);
        return await query.ToListAsync(ct);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
        => await _db.Transactions.AddAsync(transaction, ct);

    public async Task AddAllocationsAsync(IEnumerable<SaleAllocation> allocations, CancellationToken ct = default)
        => await _db.SaleAllocations.AddRangeAsync(allocations, ct);

    public Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        _db.Transactions.Update(transaction);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
