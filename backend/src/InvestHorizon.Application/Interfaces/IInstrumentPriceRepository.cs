using InvestHorizon.Domain.Entities;

namespace InvestHorizon.Application.Interfaces;

public interface IInstrumentPriceRepository
{
    /// <summary>Cached prices for every instrument currently held in the given portfolio.</summary>
    Task<IReadOnlyList<InstrumentPrice>> GetByPortfolioAsync(Guid portfolioId, CancellationToken ct = default);
    Task<InstrumentPrice?> GetByInstrumentAsync(Guid instrumentId, CancellationToken ct = default);
    Task UpsertAsync(InstrumentPrice price, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
