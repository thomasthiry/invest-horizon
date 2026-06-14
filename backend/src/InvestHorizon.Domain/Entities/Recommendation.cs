using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Domain.Entities;

public class Recommendation
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid InstrumentId { get; set; }
    public Instrument Instrument { get; set; } = null!;
    public string Source { get; set; } = string.Empty;
    public RecommendationRating Rating { get; set; }
    public DateOnly Date { get; set; }
    public decimal? TargetPrice { get; set; }
    public string? Url { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
