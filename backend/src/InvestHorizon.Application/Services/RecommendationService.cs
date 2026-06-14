using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.Services;

public sealed class RecommendationService
{
    private readonly IRecommendationRepository _recommendations;
    private readonly IInstrumentPriceHistoryRepository _priceHistory;
    private readonly IInstrumentPriceRepository _prices;

    public RecommendationService(
        IRecommendationRepository recommendations,
        IInstrumentPriceHistoryRepository priceHistory,
        IInstrumentPriceRepository prices)
    {
        _recommendations = recommendations;
        _priceHistory = priceHistory;
        _prices = prices;
    }

    public async Task<Recommendation> CreateAsync(
        string userId, Guid instrumentId, string source,
        RecommendationRating rating, DateOnly date,
        decimal? targetPrice, string? url, string? comment,
        CancellationToken ct = default)
    {
        var rec = new Recommendation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            InstrumentId = instrumentId,
            Source = source,
            Rating = rating,
            Date = date,
            TargetPrice = targetPrice,
            Url = url,
            Comment = comment,
            CreatedAt = DateTime.UtcNow
        };
        await _recommendations.AddAsync(rec, ct);
        await _recommendations.SaveChangesAsync(ct);
        return rec;
    }

    public async Task<Recommendation> UpdateAsync(
        Guid id, string userId, string source,
        RecommendationRating rating, DateOnly date,
        decimal? targetPrice, string? url, string? comment,
        CancellationToken ct = default)
    {
        var rec = await _recommendations.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Recommendation {id} not found.");
        if (rec.UserId != userId)
            throw new InvalidOperationException("Not authorised.");

        rec.Source = source;
        rec.Rating = rating;
        rec.Date = date;
        rec.TargetPrice = targetPrice;
        rec.Url = url;
        rec.Comment = comment;

        await _recommendations.SaveChangesAsync(ct);
        return rec;
    }

    public async Task DeleteAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var rec = await _recommendations.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Recommendation {id} not found.");
        if (rec.UserId != userId)
            throw new InvalidOperationException("Not authorised.");

        _recommendations.Remove(rec);
        await _recommendations.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<string>> GetDistinctSources(string userId, CancellationToken ct = default)
        => _recommendations.GetDistinctSourcesAsync(userId, ct);

    public async Task<IReadOnlyList<RecommendationWithEvaluation>> GetAllWithEvaluationAsync(
        string userId, Guid? instrumentId, string? source, CancellationToken ct = default)
    {
        var recs = await _recommendations.GetAllAsync(userId, instrumentId, source, ct);
        var results = new List<RecommendationWithEvaluation>(recs.Count);
        foreach (var rec in recs)
            results.Add(new RecommendationWithEvaluation(rec, await EvaluateAsync(rec, ct)));
        return results;
    }

    public async Task<RecommendationEvaluation?> EvaluateAsync(Recommendation rec, CancellationToken ct = default)
    {
        // Price at (or just after) the recommendation date — nearest trading day within 7 days.
        var window = await _priceHistory.GetRangeAsync(rec.InstrumentId, rec.Date, rec.Date.AddDays(7), ct);
        if (window.Count == 0) return null;

        var recPrice = window[0].CloseNative;
        if (recPrice == 0) return null;

        // Current price: prefer latest cached quote, fall back to most recent history point.
        var latest = await _prices.GetByInstrumentAsync(rec.InstrumentId, ct);
        decimal currentPrice;
        if (latest is not null)
        {
            currentPrice = latest.PriceNative;
        }
        else
        {
            var allHistory = await _priceHistory.GetRangeAsync(rec.InstrumentId, rec.Date, DateOnly.FromDateTime(DateTime.UtcNow), ct);
            if (allHistory.Count == 0) return null;
            currentPrice = allHistory[^1].CloseNative;
        }

        var returnSince = (currentPrice - recPrice) / recPrice;
        var signal = rec.Rating.Signal();

        bool? directionallyCorrect = signal == 0
            ? null
            : Math.Sign(signal) == Math.Sign(returnSince);

        double performanceScore = signal * (double)returnSince;

        bool? targetReached = null;
        if (rec.TargetPrice.HasValue && rec.TargetPrice.Value > 0)
        {
            var fullHistory = await _priceHistory.GetRangeAsync(
                rec.InstrumentId, rec.Date, DateOnly.FromDateTime(DateTime.UtcNow), ct);

            if (signal > 0)
                targetReached = fullHistory.Any(p => p.CloseNative >= rec.TargetPrice.Value);
            else if (signal < 0)
                targetReached = fullHistory.Any(p => p.CloseNative <= rec.TargetPrice.Value);
        }

        return new RecommendationEvaluation(recPrice, currentPrice, returnSince, directionallyCorrect, performanceScore, targetReached);
    }

    public async Task<IReadOnlyList<SourceScorecard>> GetScorecardAsync(string userId, CancellationToken ct = default)
    {
        var all = await GetAllWithEvaluationAsync(userId, null, null, ct);

        return all
            .GroupBy(r => r.Recommendation.Source)
            .Select(g =>
            {
                var evaluated = g.Where(r => r.Evaluation is not null).ToList();
                var withDirection = evaluated.Where(r => r.Evaluation!.DirectionallyCorrect.HasValue).ToList();

                double? hitRate = withDirection.Count == 0
                    ? null
                    : (double)withDirection.Count(r => r.Evaluation!.DirectionallyCorrect == true) / withDirection.Count;

                double? avgReturn = evaluated.Count == 0
                    ? null
                    : evaluated.Average(r => (double)r.Evaluation!.ReturnSince);

                double? avgScore = evaluated.Count == 0
                    ? null
                    : evaluated.Average(r => r.Evaluation!.PerformanceScore);

                return new SourceScorecard(
                    g.Key,
                    g.Count(),
                    evaluated.Count,
                    hitRate,
                    avgReturn,
                    avgScore);
            })
            .OrderBy(s => s.Source)
            .ToList();
    }
}

public record RecommendationEvaluation(
    decimal PriceAtRec,
    decimal CurrentPrice,
    decimal ReturnSince,
    bool? DirectionallyCorrect,
    double PerformanceScore,
    bool? TargetReached
);

public record RecommendationWithEvaluation(
    Recommendation Recommendation,
    RecommendationEvaluation? Evaluation
);

public record SourceScorecard(
    string Source,
    int TotalCount,
    int EvaluatedCount,
    double? HitRate,
    double? AvgReturn,
    double? AvgScore
);
