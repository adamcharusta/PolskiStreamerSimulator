using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3;
using PolskiStreamerSimulator.Domain.Rulesets.Shared;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets;

/// <summary>
/// Rules of M3 (<c>m3-systems.1</c>): the shared M1 core plus career flags, follow-up events and mandatory
/// decisions answered between weeks, whose cash, follower changes and rolls are carried into the next week.
/// </summary>
public sealed class M3Ruleset : IRuleset
{
    public const string RulesetId = "m3-systems.1";

    private const string FollowersEventCode = "followers:event";

    public string Id => RulesetId;

    /// <summary>
    /// Rejects while decisions are queued; otherwise applies the action, selects the events (due live follow-ups
    /// first) and moves the decision carry into the new pending week.
    /// </summary>
    public Result<CareerState> StartWeek(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion)
    {
        if (s.PendingDecisions is { Length: > 0 })
        {
            return Result<CareerState>.Fail(DomainErrorCode.DecisionPending, "decision-pending");
        }

        var applied = WeekActions.Apply(s, c, plan, expectedVersion);
        if (!applied.IsSuccess)
        {
            return Result<CareerState>.Fail(applied.Error);
        }

        var rng = applied.Value.Rng;
        var (eventIds, scheduled) = EventSelector.Select(applied.Value.State, c, plan.Category, applied.Value.State.Scheduled ?? [], ref rng);
        var started = WeekActions.Started(s, applied.Value, plan, eventIds, rng);
        var pending = started.Pending!;
        if (s.Carry is { } carry)
        {
            pending = pending with
            {
                CashDeltaFromEvents = pending.CashDeltaFromEvents + carry.CashPln,
                Lines = carry.Lines.AddRange(pending.Lines),
                FollowersPercent = carry.FollowersPercent,
                Carry = carry,
            };
        }

        return Result<CareerState>.Ok(started with { Scheduled = scheduled, Carry = null, Pending = pending });
    }

    public IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c)
        => WeekActions.LegalPlans(s, c);

    /// <summary>The next week waits while mandatory decisions are queued.</summary>
    public bool RequiresPlayerDecision(CareerState s, ContentSnapshot c)
        => s.Status == CareerStatus.Active && s.PendingDecisions is { Length: > 0 };

    public Result<EventPresentation> PresentEvent(CareerState s, ContentSnapshot c) => LiveEvents.Present(s, c);

    public Result<CareerState> ResolveEvent(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)
    {
        var resolved = LiveEvents.Resolve(s, c, choiceId, expectedVersion);
        if (!resolved.IsSuccess)
        {
            return Result<CareerState>.Fail(resolved.Error);
        }

        var (state, e) = resolved.Value;
        return Result<CareerState>.Ok(e.OneTime ? state with { UsedOneTime = UsedOnce(state, e.Id) } : state);
    }

    /// <summary>
    /// Settles like M1, then applies the week's follower percentage, expires flags and — unless the career ended —
    /// opens the decisions due before the new week. The report lists the carried decision rolls.
    /// </summary>
    public Result<(CareerState State, WeekReport Report)> SettleWeek(CareerState s, ContentSnapshot c, long expectedVersion)
    {
        var settleable = WeekSettlement.Settleable(s, expectedVersion);
        if (!settleable.IsSuccess)
        {
            return Result<(CareerState, WeekReport)>.Fail(settleable.Error);
        }

        var pending = settleable.Value;
        var core = ApplyFollowersPercent(WeekSettlement.Core(s, pending, c), pending.FollowersPercent);
        var (state, ending) = WeekSettlement.Close(s, Flags.Expire(core.State, s.Week.Value), c);
        if (ending is null)
        {
            var rng = state.Rng;
            state = DecisionQueue.Open(state, c, ref rng) with { Rng = rng };
        }

        var report = WeekSettlement.Report(s, pending, core, ending) with { DecisionRolls = pending.Carry?.Rolls ?? [] };
        return Result<(CareerState, WeekReport)>.Ok((state, report));
    }

    public Result<CareerState> Retire(CareerState s, ContentSnapshot c, long expectedVersion) => RuleSteps.Retire(s, c, expectedVersion);

    /// <summary>Odds of the first queued decision, exactly as a live event's odds are shown.</summary>
    public Result<EventPresentation> PresentDecision(CareerState s, ContentSnapshot c)
    {
        if (s.Status == CareerStatus.Ended)
        {
            return Result<EventPresentation>.Fail(DomainErrorCode.CareerEnded, "career-ended");
        }

        var decision = NextDecision(s, c);
        return decision.IsSuccess
            ? Result<EventPresentation>.Ok(LiveEvents.Presentation(s, decision.Value, c))
            : Result<EventPresentation>.Fail(decision.Error);
    }

    /// <summary>
    /// Rolls the chosen answer to the first queued decision: stat, skill, flag and follow-up effects apply now; cash,
    /// follower effects, stat lines and the roll go into the carry. Sets the cooldown and one-time use, pops the queue.
    /// </summary>
    public Result<CareerState> ResolveDecision(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)
    {
        if (RuleSteps.CommandRejection(s, expectedVersion) is { } error)
        {
            return Result<CareerState>.Fail(error);
        }

        var decision = NextDecision(s, c);
        var answer = decision.IsSuccess ? EventRolls.Answer(s, c, decision.Value, choiceId) : Result<EventAnswer>.Fail(decision.Error);
        if (!answer.IsSuccess)
        {
            return Result<CareerState>.Fail(answer.Error);
        }

        var e = answer.Value.Event;
        var state = EffectApplier.ToCarry(s, e.Id, answer.Value.Effects);
        var carry = state.Carry!;
        return Result<CareerState>.Ok(EventRolls.Recorded(s, state, answer.Value) with
        {
            Carry = carry with { Rolls = carry.Rolls.Add(answer.Value.Roll) },
            PendingDecisions = s.PendingDecisions!.Value.RemoveAt(0),
            UsedOneTime = e.OneTime ? UsedOnce(state, e.Id) : state.UsedOneTime,
        });
    }

    /// <summary>No M3 action is rolled yet.</summary>
    public ChoiceOdds? PresentAction(CareerState s, ContentSnapshot c, WeekPlan plan) => null;

    private static Result<EventDefinition> NextDecision(CareerState s, ContentSnapshot c)
        => s.PendingDecisions is { Length: > 0 } queue
            ? RuleSteps.FindEvent(c, queue[0])
            : Result<EventDefinition>.Fail(DomainErrorCode.NoPendingDecision, "no-pending-decision");

    private static ImmutableHashSet<string> UsedOnce(CareerState s, string eventId)
        => (s.UsedOneTime ?? ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal)).Add(eventId);

    /// <summary>
    /// Changes followers by <c>round(followers × pct / 100)</c> after the audience change, never below zero; the change
    /// counts as gained or lost followers and the report lists the percentage as <c>followers:event</c>.
    /// </summary>
    private static SettledCore ApplyFollowersPercent(SettledCore core, Fixed? percent)
    {
        if (percent is not { } pct || pct == Fixed.Zero)
        {
            return core;
        }

        var followers = core.State.Followers;
        var change = Math.Max(-followers, (Fixed.FromInt(followers) * pct / Fixed.Hundred).RoundToLong());
        return core with
        {
            State = core.State with { Followers = followers + change },
            Audience = core.Audience with
            {
                Gained = core.Audience.Gained + Math.Max(0, change),
                Lost = core.Audience.Lost + Math.Max(0, -change),
                Net = core.Audience.Net + change,
            },
            StatLines = core.StatLines.Add(new ReportLine(FollowersEventCode, Delta: pct)),
        };
    }
}
