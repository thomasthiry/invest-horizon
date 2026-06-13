using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Domain.Entities;

public class Instrument
{
    public Guid Id { get; set; }
    public string Isin { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public InstrumentType Type { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? Ticker { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
