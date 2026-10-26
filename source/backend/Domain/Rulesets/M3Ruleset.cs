using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Rulesets.Shared;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Settlement;

namespace PolskiStreamerSimulator.Domain.Rulesets;

/// <summary>
/// Rules of M3 (<c>m3-systems.1</c> and <c>m3-systems.2</c>): the shared M1 core plus career flags, follow-up
/// events and mandatory decisions answered between weeks, whose cash, follower changes and rolls are carried into
/// the next week. Registered as both IDs; M3-B rules are switched by balance sections, never by the ID.
/// </summary>
public sealed class M3Ruleset(string id) : IRuleset
{
    public const string SystemsV1 = "m3-systems.1";
    public const string SystemsV2 = "m3-systems.2";

    private readonly string _id = IsM3(id) ? id : throw new ArgumentException($"Unknown M3 ruleset id '{id}'.", nameof(id));

    private const string RestOnlyFlag = "sys:rest-only";
    private const string ShortStreamFlag = "sys:short-stream";
    private const string ViralClipFlag = "viral-clip";
    private const int ViralFlagWeeks = 4;
    private const string FollowersEventCode = "followers:event";
    private const string ChoiceUnavailable = "choice-unavailable";

    /// <summary>Both registered M3 rulesets, one per stored ruleset ID.</summary>
    public static IReadOnlyList<IRuleset> Versions { get; } = [new M3Ruleset(SystemsV1), new M3Ruleset(SystemsV2)];

    public string Id => _id;

    public static bool IsM3(string rulesetId) => rulesetId is SystemsV1 or SystemsV2;

    /// <summary>
    /// Rejects while decisions are queued; otherwise applies the action (and its roll), rolls the wager, selects the
    /// events (due live follow-ups first) and moves the decision carry into the new pending week.
    /// </summary>
    public Result<CareerState> StartWeek(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion)
    {
        if (Blocked(s, expectedVersion) is { } blocked)
        {
            return Result<CareerState>.Fail(blocked);
        }

        var applied = WeekActions.Apply(s, c, plan, expectedVersion, RestOnly);
        if (!applied.IsSuccess)
        {
            return Result<CareerState>.Fail(applied.Error);
        }

        var rng = applied.Value.Rng;
        var afterAction = ActionChoice(s, c, plan) is (var eventId, var choice)
            ? RollAction(s, c, plan, applied.Value, eventId, choice, ref rng)
            : WeekActions.Started(s, applied.Value, plan, [], rng);
        if (plan.Wager is not null && c.Balance.Wagers is { } wagers)
        {
            var wagered = afterAction.Pending!;
            afterAction = WagerRules.Roll(afterAction, plan, wagers, ref wagered, ref rng) with { Pending = wagered };
        }

        if (plan is { Action: MainAction.PlanPlatformMigration, TargetPlatform: { } target } && c.Balance.Migration is { } migration)
        {
            afterAction = PlatformRules.Plan(s, afterAction, target, migration, ref rng);
        }

        var (eventIds, scheduled) = EventSelector.Select(afterAction, c, plan.Category, afterAction.Scheduled ?? [], ref rng);
        var started = afterAction with
        {
            Rng = rng,
            Pending = afterAction.Pending! with
            {
                EventIds = eventIds,
                Exposure = c.Balance.Policy is null ? null : PolicyRules.Exposure(s, plan, c),
            },
        };
        var pending = started.Pending;
        if (s.Carry is { } carry)
        {
            pending = pending with
            {
                CashDeltaFromEvents = pending.CashDeltaFromEvents + carry.CashPln,
                Lines = carry.Lines.AddRange(pending.Lines),
                FollowersPercent = carry.FollowersPercent,
                Migration = carry.Migration ?? pending.Migration,
                Carry = carry,
            };
        }

        return Result<CareerState>.Ok(started with { Scheduled = scheduled, Carry = null, Pending = pending });
    }

    public IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c)
        => WeekActions.LegalPlans(s, c, RestOnly);

    /// <summary>No forced rest in M3 (the recovery crisis replaces it); a week off allows only resting.</summary>
    private static string? RestOnly(CareerState s, WeekPlan plan)
        => CareerFlags.IsActive(s, RestOnlyFlag) && plan.Action != MainAction.Rest ? "rest-only" : null;

    /// <summary>The next week waits while mandatory decisions are queued.</summary>
    public bool RequiresPlayerDecision(CareerState s, ContentSnapshot c)
        => s.Status == CareerStatus.Active && s.PendingDecisions is { Length: > 0 };

    public Result<EventPresentation> PresentEvent(CareerState s, ContentSnapshot c) => LiveEvents.Present(s, c);

    /// <summary>Resolves like M1, but rejects a choice whose requirement the career does not meet.</summary>
    public Result<CareerState> ResolveEvent(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)
    {
        if ((RuleSteps.CommandRejection(s, expectedVersion) ?? Unavailable(LiveEvents.Present(s, c), choiceId)) is { } error)
        {
            return Result<CareerState>.Fail(error);
        }

        var resolved = LiveEvents.Resolve(s, c, choiceId, expectedVersion);
        if (!resolved.IsSuccess)
        {
            return Result<CareerState>.Fail(resolved.Error);
        }

        var (state, e) = resolved.Value;
        return Result<CareerState>.Ok(e.OneTime ? state with { UsedOneTime = UsedOnce(state, e.Id) } : state);
    }

    /// <summary>
    /// Settles like M1 (after the sponsor deals and the side business, with the platform's factors), then applies the week's follower
    /// percentage, decays drama, rolls the policy strike and bans, ends the career on a global or total ban, expires flags and — unless the career ended —
    /// expires and rolls sponsor offers and opens the decisions due before the new week. The report lists the carried decision rolls.
    /// </summary>
    public Result<(CareerState State, WeekReport Report)> SettleWeek(CareerState s, ContentSnapshot c, long expectedVersion)
    {
        var settleable = WeekSettlement.Settleable(s, expectedVersion);
        if (!settleable.IsSuccess)
        {
            return Result<(CareerState, WeekReport)>.Fail(settleable.Error);
        }

        var pending = settleable.Value;
        var shortStream = CareerFlags.IsActive(s, ShortStreamFlag);
        if (shortStream)
        {
            pending = pending with { AudienceMultiplier = pending.AudienceMultiplier * c.Balance.ShortStreamAudience };
        }

        var streamed = ProgressionRules.ApplyCategoryReputation(s, pending.Plan.Category, c.Balance);
        var criticals = CriticalCount(pending);
        var (sponsored, sponsoring) = SponsorRules.Settle(
            streamed,
            pending with { Lines = pending.Lines.AddRange(Stats.Changes(s, streamed, "category")) },
            c);
        var (sideBusiness, settling) = SideBusinessRules.Settle(sponsored, sponsoring, c.Balance.SideBusiness);
        var onPlatform = c with { Balance = PlatformRules.Effective(c.Balance, s, pending.Plan.Category) };
        var core = ApplyFollowersPercent(
            WeekSettlement.Core(
                sideBusiness,
                settling,
                onPlatform,
                shortStream ? c.Balance.ShortStreamFatigue : Fixed.One,
                quality => ViralChance(c.Balance, pending.Plan.Category, quality, criticals, pending.ViralChance ?? Fixed.Zero)),
            pending.FollowersPercent);
        (core, var moved) = PlatformRules.ApplyMigration(core, pending.Migration, c, s.Week.Value);
        core = PolicyRules.DecayDrama(core, core.StatLines, pending.Plan.Category, c.Balance.Policy);
        var policyRng = core.State.Rng;
        var (policed, strike) = PolicyRules.Settle(core.State, pending.Exposure ?? 0, c, s.Week.Value, ref policyRng);
        core = core with { State = policed with { Rng = policyRng } };
        core = TrackAchievementMetrics(core, c.Balance);
        var (state, ending) = WeekSettlement.Close(s, CareerFlags.Expire(ViralFlag(core), s.Week.Value), c, PolicyRules.Ending(core.State));
        if (ending is null)
        {
            var rng = state.Rng;
            state = SponsorRules.AfterSettlement(state, c, s.Week.Value, ref rng);
            state = DecisionQueue.Open(state, c, ref rng) with { Rng = rng };
        }

        var report = WeekSettlement.Report(s, pending, core, ending) with
        {
            ActionRoll = pending.ActionRoll,
            DecisionRolls = pending.Carry?.Rolls ?? [],
            Wager = pending.Wager,
            Strike = strike,
            Migration = moved,
        };
        return Result<(CareerState, WeekReport)>.Ok((state, report));
    }

    /// <summary>M3-C: the peak drama and the paid sponsor installments, tracked only when the content has achievements.</summary>
    private static SettledCore TrackAchievementMetrics(SettledCore core, BalanceConstants b)
    {
        if (b.Achievements is null)
        {
            return core;
        }

        var metrics = core.State.Metrics;
        var paid = core.Finance.Income.Count(l => l.Code == "income:sponsor");
        return core with
        {
            State = core.State with
            {
                Metrics = metrics with
                {
                    PeakDrama = FixedMath.Max(metrics.PeakDrama ?? Fixed.Zero, core.State.Drama),
                    SponsorInstallments = (metrics.SponsorInstallments ?? 0) + paid,
                },
            },
        };
    }

    /// <summary>
    /// Category base + per-point bonus above the quality threshold + bonus per critical outcome + event effects
    /// (percentage points), kept between zero and the cap. Gameplay §11.
    /// </summary>
    public static Fixed ViralChance(BalanceConstants b, Category category, Fixed quality, int criticals, Fixed fromEffects)
    {
        var aboveThreshold = FixedMath.Max(Fixed.Zero, quality - b.ViralQualityFrom);
        var chance = b.Profile(category).ViralBase
            + (aboveThreshold * b.ViralPerQualityPoint)
            + (Fixed.FromInt(criticals) * b.ViralPerCritical)
            + (fromEffects / Fixed.Hundred);
        return chance.Clamp(Fixed.Zero, b.ViralChanceCap);
    }

    /// <summary>Critical outcomes of the week's resolved events and its action roll.</summary>
    private static int CriticalCount(PendingWeek pending)
        => pending.Resolved.Concat(pending.ActionRoll is { } action ? [action] : [])
            .Count(r => r.Outcome is RollOutcome.CriticalSuccess or RollOutcome.CriticalFailure);

    /// <summary>The state with the <c>viral-clip</c> flag set for <see cref="ViralFlagWeeks"/> weeks when the week went viral.</summary>
    private static CareerState ViralFlag(SettledCore core)
        => core.Audience.Viral is null ? core.State : CareerFlags.Set(core.State, ViralClipFlag, ViralFlagWeeks);

    /// <summary>
    /// Rejected while decisions are queued. Otherwise folds the decision carry into the career first (its cash and
    /// follower percentage would have reached the next settlement), clears the carry and the scheduled follow-ups,
    /// then retires like M1 so the final score includes them.
    /// </summary>
    public Result<CareerState> Retire(CareerState s, ContentSnapshot c, long expectedVersion)
        => Blocked(s, expectedVersion) is { } blocked
            ? Result<CareerState>.Fail(blocked)
            : RuleSteps.Retire(FoldCarry(s), c, expectedVersion);

    /// <summary>
    /// Odds of the first queued decision, exactly as a live event's odds are shown; the <c>platform-ban</c> decision
    /// also carries its definition and the forced-move estimate of each choice.
    /// </summary>
    public Result<EventPresentation> PresentDecision(CareerState s, ContentSnapshot c)
    {
        var presented = LiveEvents.Present(s, c, NextDecision(s, c));
        return presented.IsSuccess && presented.Value.EventId == PlatformRules.BanDecisionId
            ? Result<EventPresentation>.Ok(PlatformRules.PresentBan(s, c, presented.Value))
            : presented;
    }

    /// <summary>
    /// Rolls the chosen answer to the first queued decision: stat, skill, flag and follow-up effects apply now; cash,
    /// follower effects, stat lines and the roll go into the carry. Sets the cooldown and one-time use, pops the queue.
    /// A choice whose requirement the career does not meet is rejected.
    /// </summary>
    public Result<CareerState> ResolveDecision(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)
    {
        if ((RuleSteps.CommandRejection(s, expectedVersion) ?? Unavailable(PresentDecision(s, c), choiceId)) is { } error)
        {
            return Result<CareerState>.Fail(error);
        }

        if (s.PendingDecisions is { Length: > 0 } queue && queue[0] == PlatformRules.BanDecisionId)
        {
            return PlatformRules.ResolveBan(s, c, choiceId);
        }

        var decision = NextDecision(s, c);
        var answer = decision.IsSuccess ? EventRolls.Answer(s, c, decision.Value, choiceId) : Result<EventAnswer>.Fail(decision.Error);
        if (!answer.IsSuccess)
        {
            return Result<CareerState>.Fail(answer.Error);
        }

        var e = answer.Value.Event;
        var state = EffectApplier.ToCarry(s, c, e.Id, answer.Value.Effects);
        var carry = state.Carry!;
        return Result<CareerState>.Ok(EventRolls.Recorded(s, state, answer.Value) with
        {
            Carry = carry with { Rolls = carry.Rolls.Add(answer.Value.Roll) },
            PendingDecisions = s.PendingDecisions!.Value.RemoveAt(0),
            UsedOneTime = e.OneTime ? UsedOnce(state, e.Id) : state.UsedOneTime,
        });
    }

    /// <summary>The wager terms of the plan's stake, the consequences the plan would have for the sponsor deals and its policy exposure.</summary>
    public PlanPreview PresentPlan(CareerState s, ContentSnapshot c, WeekPlan plan)
        => new(
            plan.Wager is { } tier && c.Balance.Wagers is { } w ? WagerRules.Terms(tier, w) : null,
            SponsorRules.Consequences(s, c, plan),
            PolicyRules.Preview(s, plan, c),
            PlatformRules.Preview(s, c, plan));

    /// <summary>The visible odds of the action's roll (networking, or a pitch with nothing due); null for any other plan.</summary>
    public ChoiceOdds? PresentAction(CareerState s, ContentSnapshot c, WeekPlan plan)
        => ActionChoice(s, c, plan) is (_, var choice) ? ActionOdds(s, choice, c.Balance, plan) : null;

    /// <summary>
    /// The rolled choice of the plan's action: the collaboration for a network plan, the pitch for a pitch plan with
    /// no installment due (a due installment is fulfilled without a roll); null for an action without a roll.
    /// </summary>
    private static (string EventId, ChoiceDefinition Choice)? ActionChoice(CareerState s, ContentSnapshot c, WeekPlan plan)
        => plan.Action switch
        {
            MainAction.NetworkCollaborate when c.Balance.NetworkCollaborate is { } network => ("network", network),
            MainAction.PitchFulfillSponsor when c.Balance.Sponsors is { } sponsors && SponsorRules.DueThisWeek(s).IsEmpty => ("pitch", sponsors.Pitch),
            _ => null,
        };

    /// <summary>
    /// The one place an action's odds are computed: from the state before the action with the plan's category,
    /// so the odds shown are the odds rolled and the action's own energy cost does not change them.
    /// </summary>
    private static ChoiceOdds ActionOdds(CareerState before, ChoiceDefinition choice, BalanceConstants b, WeekPlan plan)
        => OddsCalculator.For(before, choice, b, plan.Category);

    /// <summary>
    /// Opens the week and rolls the action's choice with the odds shown beforehand; its effects apply to the week,
    /// the roll is recorded for the report and a critical counts in the metrics. A successful pitch then opens one
    /// offer at once.
    /// </summary>
    private static CareerState RollAction(
        CareerState s, ContentSnapshot c, WeekPlan plan, ActionApplied applied, string eventId, ChoiceDefinition choice, ref Pcg32 rng)
    {
        var odds = ActionOdds(s, choice, c.Balance, plan);
        var (roll, outcome, effects) = RuleSteps.Roll(choice, odds, ref rng);
        var started = WeekActions.Started(s, applied, plan, [], rng);
        var pending = started.Pending!;
        var state = EffectApplier.ToWeek(started, c, eventId, effects, ref pending);
        var critical = outcome is RollOutcome.CriticalSuccess or RollOutcome.CriticalFailure;
        if (plan.Action == MainAction.PitchFulfillSponsor && outcome is RollOutcome.Success or RollOutcome.CriticalSuccess)
        {
            state = SponsorRules.Pitched(state, c, ref rng);
        }

        return state with
        {
            Pending = pending with { ActionRoll = new RollResult(eventId, choice.Id, roll, odds.Threshold, odds.Modifiers, outcome) },
            Metrics = state.Metrics with { Criticals = state.Metrics.Criticals + (critical ? 1 : 0) },
        };
    }

    /// <summary>
    /// Answers an open sponsor offer between weeks, also while decisions are queued: refused during a week, for an
    /// unknown or expired offer, and on acceptance when <see cref="SponsorRules.AcceptRejection"/> gives a reason.
    /// </summary>
    public Result<CareerState> RespondOffer(CareerState s, ContentSnapshot c, string offerId, bool accept, long expectedVersion)
    {
        if (RuleSteps.CommandRejection(s, expectedVersion) is { } error)
        {
            return Result<CareerState>.Fail(error);
        }

        if (s.Pending is not null)
        {
            return Result<CareerState>.Fail(DomainErrorCode.IllegalAction, "week-in-progress");
        }

        if (s.Offers?.FirstOrDefault(o => string.Equals(o.OfferId, offerId, StringComparison.Ordinal)) is not { } offer)
        {
            return Result<CareerState>.Fail(DomainErrorCode.IllegalAction, "unknown-offer");
        }

        if (accept && SponsorRules.AcceptRejection(s, c, offer) is { } refusal)
        {
            return Result<CareerState>.Fail(DomainErrorCode.IllegalAction, refusal);
        }

        return Result<CareerState>.Ok(SponsorRules.Respond(s, c, offer, accept) with { StateVersion = s.StateVersion + 1 });
    }

    /// <summary>The first queued decision: the ruleset-built <c>platform-ban</c> decision, or the content event.</summary>
    private static Result<EventDefinition> NextDecision(CareerState s, ContentSnapshot c)
    {
        if (s.PendingDecisions is not { Length: > 0 } queue)
        {
            return Result<EventDefinition>.Fail(DomainErrorCode.NoPendingDecision, "no-pending-decision");
        }

        return queue[0] == PlatformRules.BanDecisionId ? Result<EventDefinition>.Ok(PlatformRules.BanDecision(s, c)) : RuleSteps.FindEvent(c, queue[0]);
    }

    /// <summary>
    /// <c>choice-unavailable</c> when the presented <paramref name="choiceId"/> has an unmet requirement; a failed
    /// presentation or an unknown choice is left to the resolution to report.
    /// </summary>
    private static DomainError? Unavailable(Result<EventPresentation> presented, string choiceId)
        => presented.IsSuccess
            && presented.Value.Choices.FirstOrDefault(o => o.ChoiceId == choiceId) is { UnavailableReason: not null }
                ? new DomainError(DomainErrorCode.IllegalAction, ChoiceUnavailable)
                : null;

    /// <summary>The command rejection (ended, stale version) first, then <c>decision-pending</c> while decisions are queued.</summary>
    private static DomainError? Blocked(CareerState s, long expectedVersion)
        => RuleSteps.CommandRejection(s, expectedVersion)
            ?? (s.PendingDecisions is { Length: > 0 } ? new DomainError(DomainErrorCode.DecisionPending, "decision-pending") : null);

    /// <summary>The carry's cash and follower percentage, then a carried platform move's retention (the rest become former followers).</summary>
    private static CareerState FoldCarry(CareerState s)
    {
        var carry = s.Carry ?? DecisionCarry.Empty;
        var followers = s.Followers + FollowerChange(s.Followers, carry.FollowersPercent);
        var kept = carry.Migration is { } move ? (Fixed.FromInt(followers) * move.Retention).RoundToLong() : followers;
        return s with
        {
            CashPln = s.CashPln + carry.CashPln,
            Followers = kept,
            FormerFollowers = carry.Migration is null ? s.FormerFollowers : (s.FormerFollowers ?? 0) + followers - kept,
            Carry = null,
            Scheduled = null,
        };
    }

    /// <summary><c>round(followers × pct / 100)</c>, never taking followers below zero.</summary>
    private static long FollowerChange(long followers, Fixed percent)
        => Math.Max(-followers, (Fixed.FromInt(followers) * percent / Fixed.Hundred).RoundToLong());

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
        var change = FollowerChange(followers, pct);
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
