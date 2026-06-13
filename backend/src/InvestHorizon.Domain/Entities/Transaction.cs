using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;
    public Guid InstrumentId { get; set; }
    public Instrument Instrument { get; set; } = null!;

    public Broker Broker { get; set; }
    public TransactionSide Side { get; set; }
    public DateOnly Date { get; set; }

    // Pricing in native currency
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public string Currency { get; set; } = "EUR";

    // FX: 1 EUR = FxRate units of Currency. Always 1 for EUR.
    public decimal FxRate { get; set; } = 1m;

    // Manual custody fee (droits de garde), nullable = not applicable
    public decimal? CustodyFee { get; set; }

    // Manual broker fee override; null = let engine compute it
    public decimal? ManualBrokerFee { get; set; }

    // Computed and persisted by cost engine
    public decimal AmountNative { get; set; }   // UnitPrice * Quantity
    public decimal AmountEur { get; set; }       // AmountNative / FxRate
    public decimal BrokerFee { get; set; }
    public decimal TobAmount { get; set; }
    public decimal TotalCost { get; set; }       // AmountEur + BrokerFee + TobAmount  (Buy)
    public decimal NetProceeds { get; set; }     // AmountEur - BrokerFee - TobAmount  (Sell)

    // FIFO: remaining quantity not yet consumed by sell allocations
    public decimal RemainingQuantity { get; set; }

    public ICollection<SaleAllocation> SaleAllocationsAsBuy { get; set; } = new List<SaleAllocation>();
    public ICollection<SaleAllocation> SaleAllocationsAsSell { get; set; } = new List<SaleAllocation>();
}
