using System.Collections.Immutable;
using System.Text.Json.Serialization;
using PolskiStreamerSimulator.Domain.Numerics;

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
    /// <summary>M3-C: the highest drama at any settlement; written only when the content has achievements.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Fixed? PeakDrama { get; init; }

    /// <summary>M3-C: the sponsor installments paid so far; written only when the content has achievements.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SponsorInstallments { get; init; }

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
