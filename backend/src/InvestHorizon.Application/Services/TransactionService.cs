using InvestHorizon.Application.CostEngine;
using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.Services;

public sealed class TransactionService
{
    private readonly ITransactionRepository _transactions;
    private readonly IInstrumentRepository _instruments;
    private readonly IPortfolioRepository _portfolios;
    private readonly TransactionCostEngine _costEngine;
    private readonly IFifoMatcher _fifoMatcher;

    public TransactionService(
        ITransactionRepository transactions,
        IInstrumentRepository instruments,
        IPortfolioRepository portfolios,
        TransactionCostEngine costEngine,
        IFifoMatcher fifoMatcher)
    {
        _transactions = transactions;
        _instruments = instruments;
        _portfolios = portfolios;
        _costEngine = costEngine;
        _fifoMatcher = fifoMatcher;
    }

    public async Task<Transaction> CreateAsync(
        Guid portfolioId,
        string userId,
        Guid instrumentId,
        Broker broker,
        TransactionSide side,
        DateOnly date,
        decimal unitPrice,
        decimal quantity,
        string currency,
        decimal fxRate,
        decimal? custodyFee,
        decimal? manualBrokerFee,
        CancellationToken ct = default)
    {
        var portfolio = await _portfolios.GetByIdAsync(portfolioId, userId, ct)
            ?? throw new KeyNotFoundException($"Portfolio {portfolioId} not found.");

        var instrument = await _instruments.GetByIdAsync(instrumentId, ct)
            ?? throw new KeyNotFoundException($"Instrument {instrumentId} not found.");

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            InstrumentId = instrumentId,
            Broker = broker,
            Side = side,
            Date = date,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Currency = currency,
            FxRate = fxRate,
            CustodyFee = custodyFee,
            ManualBrokerFee = manualBrokerFee,
        };

        _costEngine.Compute(tx, instrument.Type);

        if (side == TransactionSide.Sell)
        {
            var openLots = await _transactions.GetOpenBuyLotsAsync(portfolioId, instrumentId, ct);
            var allocations = _fifoMatcher.Match(tx, openLots.ToList());

            await _transactions.AddAsync(tx, ct);
            await _transactions.AddAllocationsAsync(allocations, ct);

            // Persist updated RemainingQuantity on affected buy lots
            foreach (var lot in openLots.Where(l => l.RemainingQuantity != l.Quantity))
                await _transactions.UpdateAsync(lot, ct);
        }
        else
        {
            await _transactions.AddAsync(tx, ct);
        }

        await _transactions.SaveChangesAsync(ct);
        return tx;
    }

    public CostPreview PreviewCost(
        Broker broker,
        TransactionSide side,
        decimal unitPrice,
        decimal quantity,
        decimal fxRate,
        InstrumentType instrumentType,
        decimal? manualBrokerFee = null)
        => _costEngine.Preview(broker, side, unitPrice, quantity, fxRate, instrumentType, manualBrokerFee);
}
