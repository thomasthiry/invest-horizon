using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestHorizon.Infrastructure.Persistence.Repositories;

public sealed class FxRateHistoryRepository : IFxRateHistoryRepository
{
    private readonly AppDbContext _db;
    public FxRateHistoryRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<FxRateHistory>> GetRangeAsync(
        string currency, DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _db.FxRateHistory
            .Where(r => r.Currency == currency && r.Date >= from && r.Date <= to)
            .OrderBy(r => r.Date)
            .ToListAsync(ct);

    public async Task<DateOnly?> GetLatestDateAsync(string currency, CancellationToken ct = default)
    {
        var rates = _db.FxRateHistory.Where(r => r.Currency == currency);
        return await rates.AnyAsync(ct)
            ? await rates.MaxAsync(r => r.Date, ct)
            : null;
    }

    public async Task UpsertRangeAsync(IEnumerable<FxRateHistory> rates, CancellationToken ct = default)
    {
        foreach (var rate in rates)
        {
            var existing = await _db.FxRateHistory
                .FindAsync(new object[] { rate.Currency, rate.Date }, ct);
            if (existing is null)
            {
                await _db.FxRateHistory.AddAsync(rate, ct);
            }
            else
            {
                existing.RatePerEur = rate.RatePerEur;
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
