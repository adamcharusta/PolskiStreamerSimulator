using System.Collections.Immutable;
using System.Text.Json.Serialization;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>
/// A started, unsettled week stored in the career so every step can be persisted and replayed.
/// Event effects accumulate here and are applied when the week settles.
/// </summary>
public sealed record PendingWeek(
    CareerWeek Week,
    WeekPlan Plan,
    ImmutableArray<string> EventIds,
    ImmutableArray<RollResult> Resolved,
    Fixed EventQuality,
    Fixed AudienceMultiplier,
    long CashDeltaFromEvents,
    ImmutableArray<ReportLine> Lines)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RollResult? ActionRoll { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Fixed? FollowersPercent { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Fixed? ViralChance { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DecisionCarry? Carry { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WagerResult? Wager { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Exposure { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Migration? Migration { get; init; }
}
