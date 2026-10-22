using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>
/// The detailed report of one settled week. Followers (gained, lost, net) and live viewers are separate;
/// income minus costs equals <see cref="CashDelta"/>. M3 adds the rolls of the decisions answered before the
/// week (<see cref="DecisionRolls"/>, default when none) and the roll of the week's action (<see cref="ActionRoll"/>).
/// </summary>
public sealed record WeekReport(
    CareerWeek Week,
    WeekPlan Plan,
    ImmutableArray<RollResult> Rolls,
    long LiveViewers,
    long FollowersGained,
    long FollowersLost,
    long FollowersNet,
    ImmutableArray<ReportLine> Income,
    ImmutableArray<ReportLine> Costs,
    long CashDelta,
    ImmutableArray<ReportLine> StatChanges,
    EndingReason? Ending,
    ImmutableArray<RollResult> DecisionRolls = default,
    RollResult? ActionRoll = null,
    ViralResult? Viral = null,
    StrikeRoll? Strike = null,
    WagerResult? Wager = null,
    MigrationResult? Migration = null);
