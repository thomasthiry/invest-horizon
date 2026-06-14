using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestHorizon.Infrastructure.Persistence.Repositories;

public sealed class InstrumentPriceHistoryRepository : IInstrumentPriceHistoryRepository
{
    private readonly AppDbContext _db;
    public InstrumentPriceHistoryRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<InstrumentPriceHistory>> GetRangeAsync(
        Guid instrumentId, DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _db.InstrumentPriceHistory
            .Where(p => p.InstrumentId == instrumentId && p.Date >= from && p.Date <= to)
            .OrderBy(p => p.Date)
            .ToListAsync(ct);

    public async Task<DateOnly?> GetLatestDateAsync(Guid instrumentId, CancellationToken ct = default)
    {
        var dates = _db.InstrumentPriceHistory.Where(p => p.InstrumentId == instrumentId);
        return await dates.AnyAsync(ct)
            ? await dates.MaxAsync(p => p.Date, ct)
            : null;
    }

    public async Task UpsertRangeAsync(IEnumerable<InstrumentPriceHistory> points, CancellationToken ct = default)
    {
        foreach (var point in points)
        {
            var existing = await _db.InstrumentPriceHistory
                .FindAsync(new object[] { point.InstrumentId, point.Date }, ct);
            if (existing is null)
            {
                await _db.InstrumentPriceHistory.AddAsync(point, ct);
            }
            else
            {
                existing.CloseNative = point.CloseNative;
                existing.Currency = point.Currency;
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
