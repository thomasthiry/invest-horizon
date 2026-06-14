namespace InvestHorizon.Domain.Enums;

public enum RecommendationRating
{
    Buy,
    Accumulate,
    Hold,
    Reduce,
    Sell
}

public static class RecommendationRatingExtensions
{
    /// <summary>Bullish/neutral/bearish signal: +1, 0, -1. Used to evaluate directional correctness.</summary>
    public static int Signal(this RecommendationRating rating) => rating switch
    {
        RecommendationRating.Buy        => +1,
        RecommendationRating.Accumulate => +1,
        RecommendationRating.Hold       =>  0,
        RecommendationRating.Reduce     => -1,
        RecommendationRating.Sell       => -1,
        _                               =>  0
    };
}
