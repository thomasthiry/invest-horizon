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

    public async Task<Transaction> UpdateAsync(
        Guid portfolioId,
        string userId,
        Guid id,
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

        var tx = await _transactions.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Transaction {id} not found.");

        if (tx.PortfolioId != portfolioId)
            throw new KeyNotFoundException($"Transaction {id} not found.");

        var oldInstrumentId = tx.InstrumentId;

        var instrument = await _instruments.GetByIdAsync(instrumentId, ct)
            ?? throw new KeyNotFoundException($"Instrument {instrumentId} not found.");

        tx.InstrumentId = instrumentId;
        tx.Broker = broker;
        tx.Side = side;
        tx.Date = date;
        tx.UnitPrice = unitPrice;
        tx.Quantity = quantity;
        tx.Currency = currency;
        tx.FxRate = fxRate;
        tx.CustodyFee = custodyFee;
        tx.ManualBrokerFee = manualBrokerFee;
        _costEngine.Compute(tx, instrument.Type);

        await RecomputeInstrumentAsync(portfolioId, instrumentId, ct);
        if (oldInstrumentId != instrumentId)
            await RecomputeInstrumentAsync(portfolioId, oldInstrumentId, ct);

        await _transactions.SaveChangesAsync(ct);
        return tx;
    }

    public async Task DeleteAsync(
        Guid portfolioId,
        string userId,
        Guid id,
        CancellationToken ct = default)
    {
        var portfolio = await _portfolios.GetByIdAsync(portfolioId, userId, ct)
            ?? throw new KeyNotFoundException($"Portfolio {portfolioId} not found.");

        var tx = await _transactions.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Transaction {id} not found.");

        if (tx.PortfolioId != portfolioId)
            throw new KeyNotFoundException($"Transaction {id} not found.");

        var instrumentId = tx.InstrumentId;

        await _transactions.DeleteAsync(tx, ct);

        await RecomputeInstrumentAsync(portfolioId, instrumentId, ct, excludeId: id);

        await _transactions.SaveChangesAsync(ct);
    }

    private async Task RecomputeInstrumentAsync(Guid portfolioId, Guid instrumentId, CancellationToken ct, Guid? excludeId = null)
    {
        var all = await _transactions.GetByPortfolioAndInstrumentAsync(portfolioId, instrumentId, ct);
        var remaining = excludeId.HasValue
            ? all.Where(t => t.Id != excludeId.Value).ToList()
            : all.ToList();

        var sellIds = remaining.Where(t => t.Side == TransactionSide.Sell).Select(t => t.Id);
        await _transactions.RemoveAllocationsForSellsAsync(sellIds, ct);

        var buys = remaining.Where(t => t.Side == TransactionSide.Buy).ToList();
        foreach (var buy in buys)
            buy.RemainingQuantity = buy.Quantity;

        var newAllocations = new List<SaleAllocation>();
        var sells = remaining
            .Where(t => t.Side == TransactionSide.Sell)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToList();

        foreach (var sell in sells)
        {
            var openLots = buys.Where(b => b.RemainingQuantity > 0)
                               .OrderBy(b => b.Date)
                               .ThenBy(b => b.Id)
                               .ToList();
            var allocations = _fifoMatcher.Match(sell, openLots);
            newAllocations.AddRange(allocations);
        }

        if (newAllocations.Count > 0)
            await _transactions.AddAllocationsAsync(newAllocations, ct);

        foreach (var buy in buys)
            await _transactions.UpdateAsync(buy, ct);
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
