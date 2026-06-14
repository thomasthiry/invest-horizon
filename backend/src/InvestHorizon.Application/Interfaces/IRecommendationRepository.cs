using InvestHorizon.Domain.Entities;

namespace InvestHorizon.Application.Interfaces;

public interface IRecommendationRepository
{
    Task<Recommendation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Recommendation>> GetAllAsync(string userId, Guid? instrumentId = null, string? source = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDistinctSourcesAsync(string userId, CancellationToken ct = default);
    Task AddAsync(Recommendation recommendation, CancellationToken ct = default);
    void Remove(Recommendation recommendation);
    Task SaveChangesAsync(CancellationToken ct = default);
}
