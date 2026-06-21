using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestHorizon.Infrastructure.Persistence.Repositories;

public sealed class InflationHistoryRepository : IInflationHistoryRepository
{
    private readonly AppDbContext _db;
    public InflationHistoryRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<InflationHistory>> GetRangeAsync(
        string region, DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _db.InflationHistory
            .Where(r => r.Region == region && r.Date >= from && r.Date <= to)
            .OrderBy(r => r.Date)
            .ToListAsync(ct);

    public async Task<DateOnly?> GetLatestDateAsync(string region, CancellationToken ct = default)
    {
        var rows = _db.InflationHistory.Where(r => r.Region == region);
        return await rows.AnyAsync(ct)
            ? await rows.MaxAsync(r => r.Date, ct)
            : null;
    }

    public async Task UpsertRangeAsync(IEnumerable<InflationHistory> rows, CancellationToken ct = default)
    {
        foreach (var row in rows)
        {
            var existing = await _db.InflationHistory
                .FindAsync(new object[] { row.Region, row.Date }, ct);
            if (existing is null)
                await _db.InflationHistory.AddAsync(row, ct);
            else
                existing.Index = row.Index;
        }

        await _db.SaveChangesAsync(ct);
    }
}
