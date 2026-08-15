namespace InvestHorizon.Application.Services;

/// <summary>
/// Computes which date sub-ranges of an inclusive <c>from</c>..<c>to</c> request are not yet
/// covered by the cache, given the earliest and latest dates already cached. Fills both the
/// leading gap (dates before the earliest cached day) and the trailing gap (dates after the
/// latest), so widening <c>from</c> backfills older history instead of being ignored — the
/// original logic only ever fetched forward from the latest cached date.
/// </summary>
public static class HistoryGap
{
    public static IReadOnlyList<(DateOnly From, DateOnly To)> MissingRanges(
        DateOnly from, DateOnly to, DateOnly? earliest, DateOnly? latest)
    {
        if (from > to) return Array.Empty<(DateOnly, DateOnly)>();
        if (earliest is null || latest is null) return new[] { (from, to) };

        var ranges = new List<(DateOnly, DateOnly)>(2);
        if (from < earliest.Value) ranges.Add((from, earliest.Value.AddDays(-1)));
        if (to > latest.Value) ranges.Add((latest.Value.AddDays(1), to));
        return ranges;
    }
}
