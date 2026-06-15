using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.CostEngine;

public sealed class TransactionCostEngine
{
    private readonly IEnumerable<IBrokerFeeCalculator> _feeCalculators;
    private readonly ITobCalculator _tob;

    public TransactionCostEngine(IEnumerable<IBrokerFeeCalculator> feeCalculators, ITobCalculator tob)
    {
        _feeCalculators = feeCalculators;
        _tob = tob;
    }

    /// <summary>
    /// Computes and assigns all cost fields on the transaction in-place.
    /// </summary>
    public void Compute(Transaction tx, InstrumentType instrumentType)
    {
        tx.AmountNative = tx.UnitPrice * tx.Quantity;
        tx.AmountEur = tx.FxRate == 0 ? 0m : tx.AmountNative / tx.FxRate;

        var calculator = _feeCalculators.FirstOrDefault(c => c.Broker == tx.Broker)
            ?? throw new NotSupportedException($"No fee calculator for broker {tx.Broker}.");

        tx.BrokerFee = tx.ManualBrokerFee ?? calculator.Calculate(tx.AmountEur, tx.Side, instrumentType);
        tx.TobAmount = _tob.Calculate(tx.AmountEur, instrumentType);

        if (tx.Side == TransactionSide.Buy)
        {
            tx.TotalCost = tx.AmountEur + tx.BrokerFee + tx.TobAmount;
            tx.NetProceeds = 0m;
            tx.RemainingQuantity = tx.Quantity;
        }
        else
        {
            tx.NetProceeds = tx.AmountEur - tx.BrokerFee - tx.TobAmount;
            tx.TotalCost = 0m;
            tx.RemainingQuantity = 0m;
        }
    }

    /// <summary>
    /// Returns a cost preview without mutating any entity.
    /// </summary>
    public CostPreview Preview(
        Broker broker,
        TransactionSide side,
        decimal unitPrice,
        decimal quantity,
        decimal fxRate,
        InstrumentType instrumentType,
        decimal? manualBrokerFee = null)
    {
        var amountNative = unitPrice * quantity;
        var amountEur = fxRate == 0 ? 0m : amountNative / fxRate;

        var calculator = _feeCalculators.FirstOrDefault(c => c.Broker == broker)
            ?? throw new NotSupportedException($"No fee calculator for broker {broker}.");

        var brokerFee = manualBrokerFee ?? calculator.Calculate(amountEur, side, instrumentType);
        var tob = _tob.Calculate(amountEur, instrumentType);

        var totalCost = side == TransactionSide.Buy ? amountEur + brokerFee + tob : 0m;
        var netProceeds = side == TransactionSide.Sell ? amountEur - brokerFee - tob : 0m;

        return new CostPreview(amountNative, amountEur, brokerFee, tob, totalCost, netProceeds);
    }
}

public record CostPreview(
    decimal AmountNative,
    decimal AmountEur,
    decimal BrokerFee,
    decimal TobAmount,
    decimal TotalCost,
    decimal NetProceeds
);
