using System.Collections.Immutable;

namespace PolskiStreamerSimulator.Domain.Careers;

/// <summary>Cumulative career statistics for reports and simulation analysis.</summary>
public sealed record CareerMetrics(
    long Streams,
    long TotalLiveViews,
    ImmutableDictionary<string, long> IncomeBySource,
    ImmutableDictionary<string, long> CostsBySource,
    CategoryValues<int> WeeksPerCategory,
    int EventsResolved,
    int Criticals,
    int PeakFollowersMilestone)
{
    public static CareerMetrics Empty { get; } = new(
        0,
        0,
        ImmutableDictionary<string, long>.Empty.WithComparers(StringComparer.Ordinal),
        ImmutableDictionary<string, long>.Empty.WithComparers(StringComparer.Ordinal),
        CategoryValues<int>.Uniform(0),
        0,
        0,
        0);
}
