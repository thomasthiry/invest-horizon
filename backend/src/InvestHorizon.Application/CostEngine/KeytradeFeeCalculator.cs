using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.CostEngine;

// Keytrade published fee schedule (Belgian retail, EUR orders):
// ≤ €2,500     → €7.95
// ≤ €5,000     → €14.95
// ≤ €25,000    → €19.95
// > €25,000    → 0.082%, min €19.95
// Source: Keytrade Bank fee grid (as of 2025).
// Non-EUR orders: same tiers but converted (FX conversion is handled by caller via amountEur).
public sealed class KeytradeFeeCalculator : IBrokerFeeCalculator
{
    public Broker Broker => Broker.Keytrade;

    public decimal Calculate(decimal amountEur, TransactionSide side)
    {
        if (amountEur <= 2_500m) return 7.95m;
        if (amountEur <= 5_000m) return 14.95m;
        if (amountEur <= 25_000m) return 19.95m;
        return Math.Max(amountEur * 0.00082m, 19.95m);
    }
}
