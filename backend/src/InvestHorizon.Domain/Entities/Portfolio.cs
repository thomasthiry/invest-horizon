namespace InvestHorizon.Domain.Entities;

public class Portfolio
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "EUR";

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
