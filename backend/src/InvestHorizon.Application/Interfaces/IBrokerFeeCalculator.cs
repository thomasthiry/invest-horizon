using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.Interfaces;

public interface IBrokerFeeCalculator
{
    Broker Broker { get; }
    decimal Calculate(decimal amountEur, TransactionSide side);
}
