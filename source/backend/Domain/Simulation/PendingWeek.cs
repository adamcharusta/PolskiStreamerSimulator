using System.Collections.Immutable;
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
    ImmutableArray<ReportLine> Lines);
