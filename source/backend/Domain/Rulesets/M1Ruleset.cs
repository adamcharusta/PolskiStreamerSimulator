using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Settlement;

namespace PolskiStreamerSimulator.Domain.Rulesets;

/// <summary>Rules of the M1 simulation prototype (minimal mechanics core).</summary>
public sealed class M1Ruleset : IRuleset
{
    public const string RulesetId = "m1-prototype.1";

    public string Id => RulesetId;

    public Result<CareerState> StartWeek(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion)
    {
        if (CommandRejection(s, expectedVersion) is { } error)
        {
            return Result<CareerState>.Fail(error);
        }

        if (PlanRejection(s, c.Balance, plan) is { } reason)
        {
            return Result<CareerState>.Fail(DomainErrorCode.IllegalAction, reason);
        }

        var rng = s.Rng;
        var lines = ImmutableArray.CreateBuilder<ReportLine>();
        var next = ApplyAction(s, c.Balance, plan, ref rng, lines);
        lines.AddRange(Stats.Changes(s, next, "action"));
        var eventIds = EventSelector.Select(next, c, plan.Category, ref rng);
        var pending = new PendingWeek(s.Week, plan, eventIds, [], Fixed.Zero, Fixed.One, 0, lines.ToImmutable());
        return Result<CareerState>.Ok(next with { Rng = rng, StateVersion = s.StateVersion + 1, Pending = pending });
    }

    public IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c)
        => [.. AllPlans().Select(plan => (plan, PlanRejection(s, c.Balance, plan)))];

    public Result<EventPresentation> PresentEvent(CareerState s, ContentSnapshot c)
    {
        if (s.Status == CareerStatus.Ended)
        {
            return Result<EventPresentation>.Fail(DomainErrorCode.CareerEnded, "career-ended");
        }

        var next = NextEvent(s, c);
        return next.IsSuccess
            ? Result<EventPresentation>.Ok(new EventPresentation(next.Value.Id, [.. next.Value.Choices.Select(choice => OddsCalculator.For(s, choice, c.Balance))]))
            : Result<EventPresentation>.Fail(next.Error);
    }

    public Result<CareerState> ResolveEvent(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)
    {
        if (CommandRejection(s, expectedVersion) is { } error)
        {
            return Result<CareerState>.Fail(error);
        }

        var next = NextEvent(s, c);
        if (!next.IsSuccess)
        {
            return Result<CareerState>.Fail(next.Error);
        }

        var e = next.Value;
        if (e.Choices.FirstOrDefault(choice => choice.Id == choiceId) is not { } chosen)
        {
            return Result<CareerState>.Fail(DomainErrorCode.UnknownChoice, choiceId);
        }

        var odds = OddsCalculator.For(s, chosen, c.Balance);
        var rng = s.Rng;
        var (roll, outcome, effects) = Roll(chosen, odds, ref rng);
        var pending = s.Pending!;
        var state = ApplyEffects(s, e.Id, effects, ref pending);
        var critical = outcome is RollOutcome.CriticalSuccess or RollOutcome.CriticalFailure;
        var result = new RollResult(e.Id, chosen.Id, roll, odds.Threshold, odds.Modifiers, outcome);
        return Result<CareerState>.Ok(state with
        {
            Rng = rng,
            StateVersion = s.StateVersion + 1,
            EventCooldowns = s.EventCooldowns.SetItem(e.Id, s.Week.Value + e.CooldownWeeks),
            Metrics = s.Metrics with
            {
                EventsResolved = s.Metrics.EventsResolved + 1,
                Criticals = s.Metrics.Criticals + (critical ? 1 : 0),
            },
            Pending = pending with { Resolved = pending.Resolved.Add(result) },
        });
    }

    public Result<(CareerState State, WeekReport Report)> SettleWeek(CareerState s, ContentSnapshot c, long expectedVersion)
    {
        if (CommandRejection(s, expectedVersion) is { } error)
        {
            return Result<(CareerState, WeekReport)>.Fail(error);
        }

        if (s.Pending is not { } pending)
        {
            var alreadySettled = s.LastSettledWeek >= CareerWeek.First && s.LastSettledWeek >= s.Week.Value - 1;
            return alreadySettled
                ? Result<(CareerState, WeekReport)>.Fail(DomainErrorCode.WeekAlreadySettled, $"week-{s.LastSettledWeek}")
                : Result<(CareerState, WeekReport)>.Fail(DomainErrorCode.NoPendingWeek, "no-pending-week");
        }

        if (pending.Resolved.Length < pending.EventIds.Length)
        {
            return Result<(CareerState, WeekReport)>.Fail(DomainErrorCode.EventsUnresolved, $"resolved-{pending.Resolved.Length}-of-{pending.EventIds.Length}");
        }

        var b = c.Balance;
        var primary = pending.Plan.Category;
        var rng = s.Rng;
        var statLines = pending.Lines.Where(l => l.Code.StartsWith("stat:", StringComparison.Ordinal)).ToList();

        var state = ProgressionRules.ApplyStreamFatigue(s, b);
        statLines.AddRange(Stats.Changes(s, state, "stream"));
        var audience = AudienceRules.Compute(state, pending, ref rng, b);
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
                IncomeBySource = Accumulate(s.Metrics.IncomeBySource, finance.Income),
                CostsBySource = Accumulate(s.Metrics.CostsBySource, finance.Costs),
                WeeksPerCategory = s.Metrics.WeeksPerCategory.With(primary, s.Metrics.WeeksPerCategory[primary] + 1),
            },
        };
        var report = new WeekReport(
            s.Week,
            pending.Plan,
            pending.Resolved,
            audience.LiveViewers,
            audience.Gained,
            audience.Lost,
            audience.Net,
            finance.Income,
            finance.Costs,
            finance.CashDelta,
            [.. statLines],
            null);
        return Result<(CareerState, WeekReport)>.Ok((state, report));
    }

    private static ImmutableDictionary<string, long> Accumulate(ImmutableDictionary<string, long> totals, IEnumerable<ReportLine> lines)
    {
        foreach (var line in lines)
        {
            totals = totals.SetItem(line.Code, totals.GetValueOrDefault(line.Code) + line.AmountPln!.Value);
        }

        return totals;
    }

    /// <summary>
    /// Guaranteed choices draw nothing. Otherwise roll 1–100: with criticals allowed, 1 is a critical success
    /// and 100 a critical failure; any other roll succeeds when it is at most the threshold.
    /// </summary>
    private static (int? Roll, RollOutcome Outcome, ImmutableArray<Effect> Effects) Roll(ChoiceDefinition choice, ChoiceOdds odds, ref Pcg32 rng)
    {
        var outcomes = choice.Outcomes;
        if (choice.Guaranteed)
        {
            return (null, RollOutcome.Guaranteed, outcomes.Success);
        }

        (var roll, rng) = rng.NextInt(1, 100);
        return roll switch
        {
            1 when choice.AllowsCritical => (roll, RollOutcome.CriticalSuccess, outcomes.CriticalSuccess ?? outcomes.Success),
            100 when choice.AllowsCritical => (roll, RollOutcome.CriticalFailure, outcomes.CriticalFailure ?? outcomes.Failure),
            _ when roll <= odds.Threshold => (roll, RollOutcome.Success, outcomes.Success),
            _ => (roll, RollOutcome.Failure, outcomes.Failure),
        };
    }

    /// <summary>Stat effects change the career now; cash, audience and quality effects accumulate for settlement.</summary>
    private static CareerState ApplyEffects(CareerState s, string eventId, ImmutableArray<Effect> effects, ref PendingWeek pending)
    {
        var before = s;
        var lines = pending.Lines.ToBuilder();
        foreach (var effect in effects)
        {
            switch (effect.Kind)
            {
                case EffectKind.StatDelta:
                    s = Stats.Add(s, effect.Stat!.Value, effect.Amount, pending.Plan.Category);
                    break;
                case EffectKind.CashDelta:
                    var amount = effect.Amount.RoundToLong();
                    pending = pending with { CashDeltaFromEvents = pending.CashDeltaFromEvents + amount };
                    lines.Add(new ReportLine(amount >= 0 ? "income:event" : "cost:event", AmountPln: Math.Abs(amount), Detail: eventId));
                    break;
                case EffectKind.AudienceMultiplier:
                    pending = pending with { AudienceMultiplier = pending.AudienceMultiplier * effect.Amount };
                    break;
                case EffectKind.EventQuality:
                    pending = pending with { EventQuality = pending.EventQuality + effect.Amount };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effects), effect.Kind, null);
            }
        }

        lines.AddRange(Stats.Changes(before, s, eventId));
        pending = pending with { Lines = lines.ToImmutable() };
        return s;
    }

    private static Result<EventDefinition> NextEvent(CareerState s, ContentSnapshot c)
    {
        if (s.Pending is not { } pending)
        {
            return Result<EventDefinition>.Fail(DomainErrorCode.NoPendingWeek, "no-pending-week");
        }

        if (pending.Resolved.Length >= pending.EventIds.Length)
        {
            return Result<EventDefinition>.Fail(DomainErrorCode.IllegalAction, "all-resolved");
        }

        var id = pending.EventIds[pending.Resolved.Length];
        return c.Events.FirstOrDefault(e => e.Id == id) is { } definition
            ? Result<EventDefinition>.Ok(definition)
            : Result<EventDefinition>.Fail(DomainErrorCode.ContentMismatch, $"missing-event-{id}");
    }

    private static DomainError? CommandRejection(CareerState s, long expectedVersion)
    {
        if (s.Status == CareerStatus.Ended)
        {
            return new DomainError(DomainErrorCode.CareerEnded, "career-ended");
        }

        return expectedVersion != s.StateVersion
            ? new DomainError(DomainErrorCode.StaleVersion, $"expected-{expectedVersion}-current-{s.StateVersion}")
            : null;
    }

    private static string? PlanRejection(CareerState s, BalanceConstants b, WeekPlan plan)
    {
        if (s.Status == CareerStatus.Ended)
        {
            return "career-ended";
        }

        if (s.Pending is not null)
        {
            return "week-in-progress";
        }

        if ((s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred) && plan.Action != MainAction.Rest)
        {
            return "forced-rest";
        }

        if (plan.Action != MainAction.ImproveProduction)
        {
            return null;
        }

        if (plan.UpgradeSlot is not { } slot)
        {
            return "missing-slot";
        }

        if (s.CashPln < 0)
        {
            return "negative-cash";
        }

        var tier = s.Equipment[slot];
        if (tier >= CareerInvariants.MaxEquipmentTier)
        {
            return "max-tier";
        }

        return b.EquipmentPricePln[tier + 1] > s.CashPln ? "unaffordable" : null;
    }

    private static CareerState ApplyAction(CareerState s, BalanceConstants b, WeekPlan plan, ref Pcg32 rng, ImmutableArray<ReportLine>.Builder lines)
    {
        switch (plan.Action)
        {
            case MainAction.Rest:
                s = Stats.Add(s, StatKey.Energy, b.RestEnergy, plan.Category);
                return Stats.Add(s, StatKey.Burnout, -b.RestBurnout, plan.Category);
            case MainAction.TrainCategory:
                var target = plan.TrainTarget ?? plan.Category;
                (var points, rng) = rng.NextInt(b.TrainSkillMin, b.TrainSkillMax);
                var gain = Fixed.FromInt(points) * ProgressionRules.GrowthFactor(s.Skill[target]);
                s = s with { Skill = s.Skill.With(target, Stats.Bounded(s.Skill[target] + gain)) };
                return Stats.Add(s, StatKey.Energy, -b.TrainEnergyCost, plan.Category);
            case MainAction.ImproveProduction:
                var slot = plan.UpgradeSlot!.Value;
                var tier = s.Equipment[slot] + 1;
                var price = b.EquipmentPricePln[tier];
                lines.Add(new ReportLine("cost:equipment", AmountPln: price, Detail: slot.ToString()));
                return s with { CashPln = s.CashPln - price, Equipment = s.Equipment.SetItem(slot, tier) };
            case MainAction.ManageCommunity:
                s = Stats.Add(s, StatKey.Credibility, b.ManageCommunityCredibility, plan.Category);
                s = Stats.Add(s, StatKey.Drama, -b.ManageCommunityDramaRelief, plan.Category);
                return Stats.Add(s, StatKey.PrimaryBoredom, -b.ManageCommunityBoredomRelief, plan.Category);
            case MainAction.NoAction:
                return Stats.Add(s, StatKey.Energy, b.NoActionEnergy, plan.Category);
            default:
                throw new ArgumentOutOfRangeException(nameof(plan), plan.Action, null);
        }
    }

    private static IEnumerable<WeekPlan> AllPlans()
    {
        foreach (var category in Enum.GetValues<Category>())
        {
            yield return new WeekPlan(MainAction.Rest, category);
            yield return new WeekPlan(MainAction.TrainCategory, category);
            foreach (var slot in Enum.GetValues<EquipmentSlot>())
            {
                yield return new WeekPlan(MainAction.ImproveProduction, category, UpgradeSlot: slot);
            }

            yield return new WeekPlan(MainAction.ManageCommunity, category);
            yield return new WeekPlan(MainAction.NoAction, category);
        }
    }
}
