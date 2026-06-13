using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.CostEngine;

// Revolut charges €0 explicit commission; FX spread is captured in the stored FxRate per transaction.
public sealed class RevolutFeeCalculator : IBrokerFeeCalculator
{
    public Broker Broker => Broker.Revolut;

    public decimal Calculate(decimal amountEur, TransactionSide side) => 0m;
}
