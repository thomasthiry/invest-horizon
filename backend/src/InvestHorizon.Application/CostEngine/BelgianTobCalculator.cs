using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.CostEngine;

// Belgian TOB (taxe sur opérations de bourse) — symmetric on buy and sell.
// Rates and caps per Belgian legislation (in force 2026):
//   Shares (& similar):        0.35%  cap €1,600
//   Bonds:                     0.12%  cap €1,300
//   ETFs (non-capitalizing):   0.12%  cap €1,300
//   Capitalizing funds:        1.32%  cap €4,000
public sealed class BelgianTobCalculator : ITobCalculator
{
    private static readonly Dictionary<InstrumentType, (decimal Rate, decimal Cap)> Rules = new()
    {
        [InstrumentType.Share]           = (0.0035m, 1_600m),
        [InstrumentType.Bond]            = (0.0012m, 1_300m),
        [InstrumentType.Etf]             = (0.0012m, 1_300m),
        [InstrumentType.CapitalizingFund]= (0.0132m, 4_000m),
    };

    public decimal Calculate(decimal amountEur, InstrumentType instrumentType)
    {
        var (rate, cap) = Rules[instrumentType];
        return Math.Min(Math.Round(amountEur * rate, 2), cap);
    }
}
