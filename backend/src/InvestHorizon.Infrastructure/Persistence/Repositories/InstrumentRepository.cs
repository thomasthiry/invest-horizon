using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestHorizon.Infrastructure.Persistence.Repositories;

public sealed class InstrumentRepository : IInstrumentRepository
{
    private readonly AppDbContext _db;
    public InstrumentRepository(AppDbContext db) => _db = db;

    public async Task<Instrument?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Instruments.FindAsync(new object[] { id }, ct);

    public async Task<Instrument?> GetByIsinAsync(string isin, CancellationToken ct = default)
        => await _db.Instruments.FirstOrDefaultAsync(i => i.Isin == isin, ct);

    public async Task<IReadOnlyList<Instrument>> GetAllAsync(CancellationToken ct = default)
        => await _db.Instruments.OrderBy(i => i.Name).ToListAsync(ct);

    public async Task AddAsync(Instrument instrument, CancellationToken ct = default)
        => await _db.Instruments.AddAsync(instrument, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
