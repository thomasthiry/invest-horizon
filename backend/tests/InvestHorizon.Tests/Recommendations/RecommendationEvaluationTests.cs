using FluentAssertions;
using InvestHorizon.Application.Interfaces;
using InvestHorizon.Application.Services;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Tests.Recommendations;

public class RecommendationEvaluationTests
{
    private static readonly Guid InstrumentId = Guid.NewGuid();

    private static RecommendationService BuildService(
        IReadOnlyList<InstrumentPriceHistory> history,
        InstrumentPrice? latestPrice = null)
    {
        return new RecommendationService(
            new StubRecommendationRepository(),
            new StubPriceHistoryRepository(history),
            new StubPriceRepository(latestPrice));
    }

    private static Recommendation Rec(RecommendationRating rating, DateOnly date) => new()
    {
        Id = Guid.NewGuid(),
        UserId = "u1",
        InstrumentId = InstrumentId,
        Source = "TestSource",
        Rating = rating,
        Date = date,
        CreatedAt = DateTime.UtcNow,
    };

    private static InstrumentPriceHistory Point(DateOnly date, decimal price) => new()
    {
        InstrumentId = InstrumentId,
        Date = date,
        CloseNative = price,
        Currency = "EUR",
    };

    // Convenience: build a history with a price on rec date and a later price.
    private static IReadOnlyList<InstrumentPriceHistory> TwoPoints(decimal recPrice, decimal currentPrice)
    {
        var d0 = new DateOnly(2024, 1, 2);
        return [Point(d0, recPrice), Point(d0.AddDays(30), currentPrice)];
    }

    [Fact]
    public async Task BuyRating_PriceUp_IsCorrect()
    {
        var svc = BuildService(TwoPoints(100m, 115m));
        var eval = await svc.EvaluateAsync(Rec(RecommendationRating.Buy, new DateOnly(2024, 1, 2)));

        eval.Should().NotBeNull();
        eval!.ReturnSince.Should().BeApproximately(0.15m, 0.0001m);
        eval.DirectionallyCorrect.Should().BeTrue();
        eval.PerformanceScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SellRating_PriceDown_IsCorrect()
    {
        var svc = BuildService(TwoPoints(100m, 80m));
        var eval = await svc.EvaluateAsync(Rec(RecommendationRating.Sell, new DateOnly(2024, 1, 2)));

        eval.Should().NotBeNull();
        eval!.ReturnSince.Should().BeApproximately(-0.20m, 0.0001m);
        eval.DirectionallyCorrect.Should().BeTrue();
        eval.PerformanceScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BuyRating_PriceDown_IsWrong()
    {
        var svc = BuildService(TwoPoints(100m, 90m));
        var eval = await svc.EvaluateAsync(Rec(RecommendationRating.Buy, new DateOnly(2024, 1, 2)));

        eval.Should().NotBeNull();
        eval!.DirectionallyCorrect.Should().BeFalse();
        eval.PerformanceScore.Should().BeLessThan(0);
    }

    [Fact]
    public async Task HoldRating_DirectionallyCorrect_IsNull()
    {
        var svc = BuildService(TwoPoints(100m, 105m));
        var eval = await svc.EvaluateAsync(Rec(RecommendationRating.Hold, new DateOnly(2024, 1, 2)));

        eval.Should().NotBeNull();
        eval!.DirectionallyCorrect.Should().BeNull();
    }

    [Fact]
    public async Task MissingPriceHistory_ReturnsNull()
    {
        var svc = BuildService([]);
        var eval = await svc.EvaluateAsync(Rec(RecommendationRating.Buy, new DateOnly(2024, 1, 2)));

        eval.Should().BeNull();
    }

    [Fact]
    public async Task NoLatestPrice_FallsBackToLastHistoryPoint()
    {
        var d0 = new DateOnly(2024, 1, 2);
        var history = new[]
        {
            Point(d0, 100m),
            Point(d0.AddDays(30), 110m),
        };
        // No latest price — service should use last history point
        var svc = BuildService(history, latestPrice: null);
        var eval = await svc.EvaluateAsync(Rec(RecommendationRating.Buy, d0));

        eval.Should().NotBeNull();
        eval!.CurrentPrice.Should().Be(110m);
        eval.ReturnSince.Should().BeApproximately(0.10m, 0.0001m);
    }

    // ── Stubs ──────────────────────────────────────────────────────────────────

    private sealed class StubPriceHistoryRepository : IInstrumentPriceHistoryRepository
    {
        private readonly IReadOnlyList<InstrumentPriceHistory> _data;
        public StubPriceHistoryRepository(IReadOnlyList<InstrumentPriceHistory> data) => _data = data;

        public Task<IReadOnlyList<InstrumentPriceHistory>> GetRangeAsync(
            Guid instrumentId, DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            IReadOnlyList<InstrumentPriceHistory> result = _data
                .Where(p => p.Date >= from && p.Date <= to)
                .OrderBy(p => p.Date)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<DateOnly?> GetLatestDateAsync(Guid instrumentId, CancellationToken ct = default)
        {
            var max = _data.Any() ? _data.Max(p => p.Date) : (DateOnly?)null;
            return Task.FromResult(max);
        }

        public Task<DateOnly?> GetEarliestDateAsync(Guid instrumentId, CancellationToken ct = default)
        {
            var min = _data.Any() ? _data.Min(p => p.Date) : (DateOnly?)null;
            return Task.FromResult(min);
        }

        public Task UpsertRangeAsync(IEnumerable<InstrumentPriceHistory> points, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubPriceRepository : IInstrumentPriceRepository
    {
        private readonly InstrumentPrice? _price;
        public StubPriceRepository(InstrumentPrice? price) => _price = price;

        public Task<InstrumentPrice?> GetByInstrumentAsync(Guid instrumentId, CancellationToken ct = default)
            => Task.FromResult(_price);

        public Task<IReadOnlyList<InstrumentPrice>> GetByPortfolioAsync(Guid portfolioId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InstrumentPrice>>([]);

        public Task UpsertAsync(InstrumentPrice price, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubRecommendationRepository : IRecommendationRepository
    {
        public Task<Recommendation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Recommendation?>(null);
        public Task<IReadOnlyList<Recommendation>> GetAllAsync(string userId, Guid? instrumentId = null, string? source = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Recommendation>>([]);
        public Task<IReadOnlyList<string>> GetDistinctSourcesAsync(string userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
        public Task AddAsync(Recommendation recommendation, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(Recommendation recommendation) { }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
