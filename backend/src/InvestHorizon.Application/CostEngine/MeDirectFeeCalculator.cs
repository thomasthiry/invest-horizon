using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.CostEngine;

// MeDirect fee grid — in force 2026. Fee depends on the instrument type:
//   ETF              → €0 (free on all exchanges, permanent since Aug 2025)
//   Share            → 0.15%, min €7
//   Bond             → 0.15%, min €15
//   CapitalizingFund → treated as a share (0.15%, min €7) — assumption, no published distinct rate
// Source: MeDirect Bank tariff document (Tariffs & charges, in force 01/04/2026).
// Scope: Euronext only. The US $7 minimum is not modeled — use ManualBrokerFee for those.
// FX conversion is handled by the caller via amountEur.
public sealed class MeDirectFeeCalculator : IBrokerFeeCalculator
{
    public Broker Broker => Broker.MeDirect;

    public decimal Calculate(decimal amountEur, TransactionSide side, InstrumentType instrumentType)
        => instrumentType switch
        {
            InstrumentType.Etf => 0m,
            InstrumentType.Bond => Math.Max(Math.Round(amountEur * 0.0015m, 2), 15m),
            _ => Math.Max(Math.Round(amountEur * 0.0015m, 2), 7m),
        };
}
