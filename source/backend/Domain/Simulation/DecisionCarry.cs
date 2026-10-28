using System.Collections.Immutable;
using System.Text.Json.Serialization;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>
/// Results of decisions answered between weeks that belong to the next week's report: their rolls, cash and
/// follower changes and stat lines. The next <c>StartWeek</c> moves the carry into the pending week.
/// </summary>
public sealed record DecisionCarry(ImmutableArray<RollResult> Rolls, long CashPln, Fixed FollowersPercent, ImmutableArray<ReportLine> Lines)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Migration? Migration { get; init; }

    public static DecisionCarry Empty { get; } = new([], 0, Fixed.Zero, []);
}
