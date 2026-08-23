using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.CostEngine;

/// <summary>
/// Estimates what it would cost to liquidate an open position today.
/// <para>
/// The position is assumed to be sold in <b>one order per broker</b>: every open buy lot held at the
/// same broker is closed by a single sell operation, so that broker's fee grid is applied once to the
/// combined order value (which matters because both the Keytrade grid and the MeDirect/Revolut minimums
/// are order-value based). A position spread across two brokers costs two orders, and the fees of both
/// are summed. Belgian TOB is likewise charged per order, so its per-order cap applies per broker.
/// </para>
/// </summary>
public sealed class ExitCostEstimator
{
    private readonly IEnumerable<IBrokerFeeCalculator> _feeCalculators;
    private readonly ITobCalculator _tob;

    public ExitCostEstimator(IEnumerable<IBrokerFeeCalculator> feeCalculators, ITobCalculator tob)
    {
        _feeCalculators = feeCalculators;
        _tob = tob;
    }

    /// <summary>
    /// Broker fees + TOB payable to close <paramref name="openBuyLots"/> at <paramref name="unitPriceEur"/>,
    /// broken down into the one sell order each broker would receive.
    /// Only <see cref="Transaction.RemainingQuantity"/> is considered; lots with nothing left are ignored.
    /// </summary>
    public ExitCostEstimate Estimate(
        IEnumerable<Transaction> openBuyLots,
        decimal unitPriceEur,
        InstrumentType instrumentType)
    {
        var orders = new List<ExitCostOrder>();

        foreach (var perBroker in openBuyLots.GroupBy(l => l.Broker))
        {
            var quantity = perBroker.Sum(l => l.RemainingQuantity);
            if (quantity <= 0m) continue;

            var orderValueEur = quantity * unitPriceEur;
            if (orderValueEur <= 0m) continue;

            var calculator = _feeCalculators.FirstOrDefault(c => c.Broker == perBroker.Key)
                ?? throw new NotSupportedException($"No fee calculator for broker {perBroker.Key}.");

            orders.Add(new ExitCostOrder(
                Broker: perBroker.Key,
                Quantity: quantity,
                UnitPriceEur: unitPriceEur,
                OrderValueEur: orderValueEur,
                BrokerFeeEur: calculator.Calculate(orderValueEur, TransactionSide.Sell, instrumentType),
                TobEur: _tob.Calculate(orderValueEur, instrumentType)));
        }

        // Stable, predictable order for the UI: biggest order first.
        orders.Sort((a, b) => b.OrderValueEur.CompareTo(a.OrderValueEur));

        return new ExitCostEstimate(orders, orders.Sum(o => o.TotalEur));
    }
}

/// <summary>The single sell order one broker would receive to close its share of a position.</summary>
public record ExitCostOrder(
    Broker Broker,
    decimal Quantity,
    decimal UnitPriceEur,
    decimal OrderValueEur,
    decimal BrokerFeeEur,
    decimal TobEur
)
{
    public decimal TotalEur => BrokerFeeEur + TobEur;
}

public record ExitCostEstimate(IReadOnlyList<ExitCostOrder> Orders, decimal TotalEur);
