using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>
/// The detailed report of one settled week. Followers (gained, lost, net) and live viewers are separate;
/// income minus costs equals <see cref="CashDelta"/>.
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
    EndingReason? Ending);
