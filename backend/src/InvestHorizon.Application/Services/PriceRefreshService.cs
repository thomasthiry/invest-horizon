using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace InvestHorizon.Application.Services;

/// <summary>
/// On-demand refresh of cached market prices for every instrument currently held in a portfolio.
/// Resilient: a failure for one instrument does not abort the batch.
/// </summary>
public sealed class PriceRefreshService
{
    private readonly ITransactionRepository _transactions;
    private readonly IInstrumentRepository _instruments;
    private readonly IInstrumentPriceRepository _prices;
    private readonly IPriceProvider _provider;
    private readonly ILogger<PriceRefreshService> _logger;

    public PriceRefreshService(
        ITransactionRepository transactions,
        IInstrumentRepository instruments,
        IInstrumentPriceRepository prices,
        IPriceProvider provider,
        ILogger<PriceRefreshService> logger)
    {
        _transactions = transactions;
        _instruments = instruments;
        _prices = prices;
        _provider = provider;
        _logger = logger;
    }

    public async Task<PriceRefreshResult> RefreshPortfolioAsync(Guid portfolioId, CancellationToken ct = default)
    {
        var txs = await _transactions.GetByPortfolioAsync(portfolioId, ct);
        var heldInstrumentIds = txs
            .Where(t => t.Side == TransactionSide.Buy && t.RemainingQuantity > 0)
            .Select(t => t.InstrumentId)
            .Distinct()
            .ToList();

        int succeeded = 0, failed = 0;
        foreach (var instrumentId in heldInstrumentIds)
        {
            ct.ThrowIfCancellationRequested();
            var instrument = await _instruments.GetByIdAsync(instrumentId, ct);
            if (instrument is null) { failed++; continue; }

            try
            {
                var quote = await _provider.GetQuoteAsync(instrument, ct);
                if (quote is null)
                {
                    _logger.LogWarning("No quote resolved for {Isin} ({Name})", instrument.Isin, instrument.Name);
                    failed++;
                    continue;
                }

                // Cache the resolved symbol so future refreshes skip the ISIN search.
                if (!string.IsNullOrWhiteSpace(quote.Symbol) && instrument.PriceSymbol != quote.Symbol)
                    instrument.PriceSymbol = quote.Symbol;

                await _prices.UpsertAsync(new InstrumentPrice
                {
                    InstrumentId = instrument.Id,
                    PriceNative = quote.Price,
                    Currency = quote.Currency,
                    AsOf = quote.AsOf,
                    FetchedAt = DateTime.UtcNow,
                    Source = quote.Source
                }, ct);
                succeeded++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch price for {Isin} ({Name})", instrument.Isin, instrument.Name);
                failed++;
            }
        }

        await _instruments.SaveChangesAsync(ct);
        await _prices.SaveChangesAsync(ct);

        return new PriceRefreshResult(heldInstrumentIds.Count, succeeded, failed);
    }
}

public record PriceRefreshResult(int Total, int Succeeded, int Failed);
