using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvestHorizon.Infrastructure.Persistence.Repositories;

public sealed class InstrumentPriceRepository : IInstrumentPriceRepository
{
    private readonly AppDbContext _db;
    public InstrumentPriceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<InstrumentPrice>> GetByPortfolioAsync(Guid portfolioId, CancellationToken ct = default)
    {
        var heldInstrumentIds = _db.Transactions
            .Where(t => t.PortfolioId == portfolioId && t.Side == TransactionSide.Buy && t.RemainingQuantity > 0)
            .Select(t => t.InstrumentId)
            .Distinct();

        return await _db.InstrumentPrices
            .Where(p => heldInstrumentIds.Contains(p.InstrumentId))
            .ToListAsync(ct);
    }

    public async Task<InstrumentPrice?> GetByInstrumentAsync(Guid instrumentId, CancellationToken ct = default)
        => await _db.InstrumentPrices.FindAsync(new object[] { instrumentId }, ct);

    public async Task UpsertAsync(InstrumentPrice price, CancellationToken ct = default)
    {
        var existing = await _db.InstrumentPrices.FindAsync(new object[] { price.InstrumentId }, ct);
        if (existing is null)
        {
            await _db.InstrumentPrices.AddAsync(price, ct);
        }
        else
        {
            existing.PriceNative = price.PriceNative;
            existing.Currency = price.Currency;
            existing.AsOf = price.AsOf;
            existing.FetchedAt = price.FetchedAt;
            existing.Source = price.Source;
        }
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
