using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.CostEngine;

// Revolut commission (post-allowance) on the Standard/Plus/Premium/Metal plans: 0.25% of the
// order value, minimum €1. Same for shares and ETFs.
// The monthly free-trade allowance is stateful and NOT modeled here; the Ultra / Trading Pro
// rate (0.12%, no minimum) is also not modeled — use ManualBrokerFee for those cases.
// The FX spread is still captured in the stored FxRate per transaction.
public sealed class RevolutFeeCalculator : IBrokerFeeCalculator
{
    public Broker Broker => Broker.Revolut;

    public decimal Calculate(decimal amountEur, TransactionSide side, InstrumentType instrumentType)
        => Math.Max(Math.Round(amountEur * 0.0025m, 2), 1m);
}
