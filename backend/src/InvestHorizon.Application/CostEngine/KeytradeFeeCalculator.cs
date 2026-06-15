using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.CostEngine;

// Keytrade Euronext (Brussels/Amsterdam/Paris) fee grid — in force since 22 Jan 2025.
// Shares and ETFs/trackers share the same grid on Euronext.
//   ≤ €250      → €2.45
//   ≤ €2,500    → €5.95
//   ≤ €10,000   → €14.95
//   > €10,000   → €14.95 + €7.50 per additional €10,000 block
// Source: Keytrade Bank 2025 tariff document (Tarifs 0005/FNE12/2024).
// Scope: Euronext only. Other exchanges (XETRA, US, CHF) are not modeled —
// use ManualBrokerFee for those. FX conversion is handled by the caller via amountEur.
public sealed class KeytradeFeeCalculator : IBrokerFeeCalculator
{
    public Broker Broker => Broker.Keytrade;

    public decimal Calculate(decimal amountEur, TransactionSide side, InstrumentType instrumentType)
    {
        if (amountEur <= 250m) return 2.45m;
        if (amountEur <= 2_500m) return 5.95m;
        if (amountEur <= 10_000m) return 14.95m;

        var extraBlocks = (int)Math.Ceiling((amountEur - 10_000m) / 10_000m);
        return 14.95m + extraBlocks * 7.50m;
    }
}
