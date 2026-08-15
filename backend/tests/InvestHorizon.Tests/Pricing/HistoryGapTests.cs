using FluentAssertions;
using InvestHorizon.Application.Services;
using Xunit;

namespace InvestHorizon.Tests.Pricing;

public class HistoryGapTests
{
    private static DateOnly D(int m, int d) => new(2025, m, d);

    [Fact]
    public void EmptyCache_FetchesWholeRange()
    {
        var gaps = HistoryGap.MissingRanges(D(11, 1), D(12, 1), earliest: null, latest: null);
        gaps.Should().ContainSingle().Which.Should().Be((D(11, 1), D(12, 1)));
    }

    [Fact]
    public void RequestWithinCache_FetchesNothing()
    {
        var gaps = HistoryGap.MissingRanges(D(11, 10), D(11, 20), earliest: D(11, 1), latest: D(12, 1));
        gaps.Should().BeEmpty();
    }

    [Fact]
    public void RequestStartsBeforeCache_BackfillsLeadingGap()
    {
        // The original forward-only logic ignored this; the leading gap must now be fetched.
        var gaps = HistoryGap.MissingRanges(D(11, 1), D(12, 1), earliest: D(11, 20), latest: D(12, 1));
        gaps.Should().ContainSingle().Which.Should().Be((D(11, 1), D(11, 19)));
    }

    [Fact]
    public void RequestEndsAfterCache_FetchesTrailingGap()
    {
        var gaps = HistoryGap.MissingRanges(D(11, 1), D(12, 10), earliest: D(11, 1), latest: D(12, 1));
        gaps.Should().ContainSingle().Which.Should().Be((D(12, 2), D(12, 10)));
    }

    [Fact]
    public void RequestExtendsBothSides_FetchesLeadingAndTrailingGaps()
    {
        var gaps = HistoryGap.MissingRanges(D(11, 1), D(12, 10), earliest: D(11, 20), latest: D(12, 1));
        gaps.Should().BeEquivalentTo(new[]
        {
            (D(11, 1), D(11, 19)),
            (D(12, 2), D(12, 10)),
        });
    }

    [Fact]
    public void InvertedRange_FetchesNothing()
    {
        var gaps = HistoryGap.MissingRanges(D(12, 1), D(11, 1), earliest: null, latest: null);
        gaps.Should().BeEmpty();
    }
}
