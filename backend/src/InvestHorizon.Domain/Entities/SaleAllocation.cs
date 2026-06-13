namespace InvestHorizon.Domain.Entities;

public class SaleAllocation
{
    public Guid Id { get; set; }
    public Guid BuyTransactionId { get; set; }
    public Transaction BuyTransaction { get; set; } = null!;
    public Guid SellTransactionId { get; set; }
    public Transaction SellTransaction { get; set; } = null!;

    public decimal Quantity { get; set; }

    // Realized gain in EUR for this allocation: sell proceeds share − buy cost basis share
    public decimal RealizedGainEur { get; set; }

    // Year the sale occurred — denormalized for annual aggregation queries
    public int SaleYear { get; set; }
}
