using InvestHorizon.Domain.Entities;

namespace InvestHorizon.Application.Interfaces;

public interface IInstrumentRepository
{
    Task<Instrument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Instrument?> GetByIsinAsync(string isin, CancellationToken ct = default);
    Task<IReadOnlyList<Instrument>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Instrument instrument, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
