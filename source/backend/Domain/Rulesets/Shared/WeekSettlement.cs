using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Scoring;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Settlement;

namespace PolskiStreamerSimulator.Domain.Rulesets.Shared;

/// <summary>
/// The settled week before its ending check: the advanced state, the audience and finance results and the report's
/// stat lines. A ruleset may adjust it (M3 applies follower percentages) before <see cref="WeekSettlement.Close"/>.
/// </summary>
public sealed record SettledCore(CareerState State, AudienceResult Audience, FinanceResult Finance, ImmutableArray<ReportLine> StatLines);

/// <summary>
/// Settlement steps shared by the M1 and M3 rulesets: fatigue, audience, finance, practice and metrics
/// (<see cref="Core"/>), then the ending check (<see cref="Close"/>) and the report (<see cref="Report"/>).
/// </summary>
public static class WeekSettlement
{
    /// <summary>The pending week when it may settle now; otherwise the stable rejection.</summary>
    public static Result<PendingWeek> Settleable(CareerState s, long expectedVersion)
    {
        if (RuleSteps.CommandRejection(s, expectedVersion) is { } error)
        {
            return Result<PendingWeek>.Fail(error);
        }

        if (s.Pending is not { } pending)
        {
            var alreadySettled = s.LastSettledWeek >= CareerWeek.First && s.LastSettledWeek >= s.Week.Value - 1;
            return alreadySettled
                ? Result<PendingWeek>.Fail(DomainErrorCode.WeekAlreadySettled, $"week-{s.LastSettledWeek}")
                : Result<PendingWeek>.Fail(DomainErrorCode.NoPendingWeek, "no-pending-week");
        }

        return pending.Resolved.Length < pending.EventIds.Length
            ? Result<PendingWeek>.Fail(DomainErrorCode.EventsUnresolved, $"resolved-{pending.Resolved.Length}-of-{pending.EventIds.Length}")
            : Result<PendingWeek>.Ok(pending);
    }

    /// <summary>Stream fatigue (scaled by <paramref name="fatigueFactor"/>, one for a regular stream; <paramref name="viralChanceAt"/> maps the settled stream quality to a viral chance, none draws no viral roll), audience, finance, practice and metrics; advances the week and clears the pending week.</summary>
    public static SettledCore Core(CareerState s, PendingWeek pending, ContentSnapshot c, Fixed fatigueFactor, Func<Fixed, Fixed>? viralChanceAt = null)
    {
        var b = c.Balance;
        var primary = pending.Plan.Category;
        var rng = s.Rng;
        var statLines = pending.Lines.Where(l => l.Code.StartsWith("stat:", StringComparison.Ordinal)).ToList();

        var state = ProgressionRules.ApplyStreamFatigue(s, b, fatigueFactor);
        statLines.AddRange(Stats.Changes(s, state, "stream"));
        var audience = AudienceRules.Compute(state, pending, ref rng, b, viralChanceAt?.Invoke(AudienceRules.Quality(state, pending, b)) ?? Fixed.Zero);
        var finance = FinanceRules.Compute(state, pending, audience.LiveViewers, b);
        var paidAtStart = pending.Lines.Where(l => l.Code == "cost:equipment").Sum(l => l.AmountPln!.Value);
        var practiced = ProgressionRules.ApplySkillAndBoredom(state, primary, c.Profiles[s.Background].Skill, ref rng, b);
        statLines.AddRange(Stats.Changes(state, practiced, "practice"));

        var nextWeek = s.Week.IsLast ? s.Week : s.Week.Next();
        state = practiced with
        {
            Followers = Math.Max(0, s.Followers + audience.Net),
            LastLiveViewers = audience.LiveViewers,

            // The equipment purchase was paid when the week started; the report still lists it.
            CashPln = s.CashPln + finance.CashDelta + paidAtStart,
            Week = nextWeek,
            LastSettledWeek = s.Week.Value,
            Rng = rng,
            StateVersion = s.StateVersion + 1,
            Pending = null,
            EventCooldowns = s.EventCooldowns.RemoveRange(s.EventCooldowns.Where(p => p.Value <= nextWeek.Value).Select(p => p.Key)),
            Metrics = s.Metrics with
            {
                Streams = s.Metrics.Streams + 1,
                TotalLiveViews = s.Metrics.TotalLiveViews + audience.LiveViewers,
                IncomeBySource = RuleSteps.Accumulate(s.Metrics.IncomeBySource, finance.Income),
                CostsBySource = RuleSteps.Accumulate(s.Metrics.CostsBySource, finance.Costs),
                WeeksPerCategory = s.Metrics.WeeksPerCategory.With(primary, s.Metrics.WeeksPerCategory[primary] + 1),
            },
        };
        return new SettledCore(state, audience, finance, [.. statLines]);
    }

    /// <summary>
    /// Checks the ending (insolvency after all income and costs, then the ruleset's own <paramref name="ruleEnding"/>,
    /// then completion), evaluates achievements and ends the career in the settled week when it is over.
    /// <paramref name="s"/> is the state before settlement.
    /// </summary>
    public static (CareerState State, EndingReason? Ending) Close(CareerState s, CareerState state, ContentSnapshot c, EndingReason? ruleEnding = null)
    {
        EndingReason? ending = null;
        if (state.CashPln <= c.Balance.InsolvencyThresholdPln)
        {
            ending = EndingReason.Insolvency;
        }
        else if (ruleEnding is not null)
        {
            ending = ruleEnding;
        }
        else if (s.Week.IsLast)
        {
            ending = EndingReason.Completed;
        }

        state = state with
        {
            Achievements = Achievements.Evaluate(state, completed: ending == EndingReason.Completed, c.Balance.Achievements),
            Metrics = state.Metrics with
            {
                PeakFollowersMilestone = Math.Max(state.Metrics.PeakFollowersMilestone, Achievements.FollowersMilestone(state.Followers)),
            },
        };
        if (ending is { } reason)
        {
            state = RuleSteps.End(state, reason, c) with { Week = s.Week };
        }

        return (state, ending);
    }

    public static WeekReport Report(CareerState s, PendingWeek pending, SettledCore core, EndingReason? ending) => new(
        s.Week,
        pending.Plan,
        pending.Resolved,
        core.Audience.LiveViewers,
        core.Audience.Gained,
        core.Audience.Lost,
        core.Audience.Net,
        core.Finance.Income,
        core.Finance.Costs,
        core.Finance.CashDelta,
        core.StatLines,
        ending,
        Viral: core.Audience.Viral);
}
