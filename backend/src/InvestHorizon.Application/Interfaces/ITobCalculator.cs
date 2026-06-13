using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.Interfaces;

public interface ITobCalculator
{
    decimal Calculate(decimal amountEur, InstrumentType instrumentType);
}
