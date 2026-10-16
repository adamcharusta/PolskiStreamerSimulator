using System.Collections.Immutable;
using System.Text.Json.Serialization;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Scoring;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Careers;

/// <summary>Complete immutable state of one career; every accepted command produces a new instance.</summary>
public sealed record CareerState
{
    public required string RulesetId { get; init; }

    public required string ContentDigest { get; init; }

    /// <summary>Incremented once per accepted state-changing command.</summary>
    public required long StateVersion { get; init; }

    public required Background Background { get; init; }

    public required Difficulty Difficulty { get; init; }

    public required PronounSet Pronouns { get; init; }

    public required CareerWeek Week { get; init; }

    /// <summary>Last week whose settlement completed; 0 before the first settlement.</summary>
    public int LastSettledWeek { get; init; }

    public required Pcg32 Rng { get; init; }

    public required long CashPln { get; init; }

    public required long Followers { get; init; }

    public required long LastLiveViewers { get; init; }

    public required Fixed Drama { get; init; }

    public required Fixed Credibility { get; init; }

    public required Fixed Energy { get; init; }

    public required Fixed Burnout { get; init; }

    public required CategoryValues<Fixed> Skill { get; init; }

    public required CategoryValues<Fixed> Boredom { get; init; }

    public required CategoryValues<int> InactiveWeeks { get; init; }

    public required ImmutableDictionary<EquipmentSlot, int> Equipment { get; init; }

    /// <summary>Event ID → first week in which the event may be selected again.</summary>
    public required ImmutableDictionary<string, int> EventCooldowns { get; init; }

    public required ImmutableHashSet<string> Achievements { get; init; }

    public required CareerMetrics Metrics { get; init; }

    public required CareerStatus Status { get; init; }

    public EndingReason? Ending { get; init; }

    public ScoreBreakdown? FinalScore { get; init; }

    public PendingWeek? Pending { get; init; }

    /// <summary>M3: flag → last week in which it is active (<see cref="CareerWeek.Last"/> for permanent).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<string, int>? Flags { get; init; }

    /// <summary>M3: follow-up events waiting for their due week, in scheduling order.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<ScheduledEvent>? Scheduled { get; init; }

    /// <summary>M3: mandatory decisions to answer before the next week starts, in order.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<string>? PendingDecisions { get; init; }

    /// <summary>M3: decision results waiting for the next week's report.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DecisionCarry? Carry { get; init; }

    /// <summary>M3: one-time events already shown.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableHashSet<string>? UsedOneTime { get; init; }
}
