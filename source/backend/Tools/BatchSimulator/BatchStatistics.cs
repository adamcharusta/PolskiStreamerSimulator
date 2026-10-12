using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.BatchSimulator;

/// <summary>p5, median, p95 and maximum by the nearest-rank method.</summary>
public sealed record Distribution(long P5, long P50, long P95, long Max);

public sealed record GroupSummary(
    string Group,
    int Careers,
    int Completed,
    int Insolvent,
    int Retired,
    Distribution Followers,
    Distribution Cash,
    Distribution Score);

public static class BatchStatistics
{
    /// <summary>Nearest-rank percentile of an ascending list: the value at rank <c>ceil(p × n / 100)</c>.</summary>
    public static long Percentile(IReadOnlyList<long> ascending, int percent)
        => ascending.Count == 0 ? 0 : ascending[Math.Max(0, ((percent * ascending.Count) + 99) / 100 - 1)];

    public static Distribution Of(IEnumerable<long> values)
    {
        var ascending = values.Order().ToArray();
        return new Distribution(Percentile(ascending, 5), Percentile(ascending, 50), Percentile(ascending, 95), ascending.Length == 0 ? 0 : ascending[^1]);
    }

    /// <summary>Groups careers in order of first appearance by index.</summary>
    public static IReadOnlyList<GroupSummary> By(IEnumerable<CareerSummary> careers, Func<CareerSummary, string> key)
        => [.. careers.OrderBy(c => c.Index).GroupBy(key).Select(g => Summarise(g.Key, [.. g]))];

    public static GroupSummary Summarise(string group, IReadOnlyList<CareerSummary> careers) => new(
        group,
        careers.Count,
        careers.Count(c => c.Ending == EndingReason.Completed),
        careers.Count(c => c.Ending == EndingReason.Insolvency),
        careers.Count(c => c.Ending == EndingReason.EarlyRetirement),
        Of(careers.Select(c => c.Followers)),
        Of(careers.Select(c => c.Cash)),
        Of(careers.Select(c => c.Score)));
}
