using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestHorizon.Infrastructure.Persistence.Repositories;

public sealed class RecommendationRepository : IRecommendationRepository
{
    private readonly AppDbContext _db;
    public RecommendationRepository(AppDbContext db) => _db = db;

    public async Task<Recommendation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Recommendations
            .Include(r => r.Instrument)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Recommendation>> GetAllAsync(
        string userId, Guid? instrumentId = null, string? source = null, CancellationToken ct = default)
    {
        var q = _db.Recommendations
            .Include(r => r.Instrument)
            .Where(r => r.UserId == userId);

        if (instrumentId.HasValue)
            q = q.Where(r => r.InstrumentId == instrumentId.Value);

        if (!string.IsNullOrWhiteSpace(source))
            q = q.Where(r => r.Source == source);

        return await q.OrderByDescending(r => r.Date).ThenByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetDistinctSourcesAsync(string userId, CancellationToken ct = default)
        => await _db.Recommendations
            .Where(r => r.UserId == userId)
            .Select(r => r.Source)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);

    public async Task AddAsync(Recommendation recommendation, CancellationToken ct = default)
        => await _db.Recommendations.AddAsync(recommendation, ct);

    public void Remove(Recommendation recommendation)
        => _db.Recommendations.Remove(recommendation);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
