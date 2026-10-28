using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Rulesets.Shared;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets;

/// <summary>Rules of the M1 simulation prototype (minimal mechanics core).</summary>
public sealed class M1Ruleset : IRuleset
{
    public const string RulesetId = "m1-prototype.1";

    public string Id => RulesetId;

    public Result<CareerState> StartWeek(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion)
    {
        var applied = WeekActions.Apply(s, c, plan, expectedVersion, ForcedRest);
        if (!applied.IsSuccess)
        {
            return Result<CareerState>.Fail(applied.Error);
        }

        var rng = applied.Value.Rng;
        var eventIds = EventSelector.Select(applied.Value.State, c, plan.Category, ref rng);
        return Result<CareerState>.Ok(WeekActions.Started(s, applied.Value, plan, eventIds, rng));
    }

    public IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c)
        => WeekActions.LegalPlans(s, c, ForcedRest);

    /// <summary>At zero energy or full burnout only resting is legal.</summary>
    private static string? ForcedRest(CareerState s, WeekPlan plan)
        => (s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred) && plan.Action != MainAction.Rest ? "forced-rest" : null;

    /// <summary>The M1 rules have no mandatory decisions (TD-90); forced rest leaves only one legal plan.</summary>
    public bool RequiresPlayerDecision(CareerState s, ContentSnapshot c) => false;

    public Result<EventPresentation> PresentEvent(CareerState s, ContentSnapshot c) => LiveEvents.Present(s, c);

    public Result<CareerState> ResolveEvent(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)
    {
        var resolved = LiveEvents.Resolve(s, c, choiceId, expectedVersion);
        return resolved.IsSuccess ? Result<CareerState>.Ok(resolved.Value.State) : Result<CareerState>.Fail(resolved.Error);
    }

    public Result<(CareerState State, WeekReport Report)> SettleWeek(CareerState s, ContentSnapshot c, long expectedVersion)
    {
        var settleable = WeekSettlement.Settleable(s, expectedVersion);
        if (!settleable.IsSuccess)
        {
            return Result<(CareerState, WeekReport)>.Fail(settleable.Error);
        }

        var pending = settleable.Value;
        var core = WeekSettlement.Core(s, pending, c, Fixed.One);
        var (state, ending) = WeekSettlement.Close(s, core.State, c);
        return Result<(CareerState, WeekReport)>.Ok((state, WeekSettlement.Report(s, pending, core, ending)));
    }

    public Result<CareerState> Retire(CareerState s, ContentSnapshot c, long expectedVersion) => RuleSteps.Retire(s, c, expectedVersion);

    public Result<EventPresentation> PresentDecision(CareerState s, ContentSnapshot c)
        => Result<EventPresentation>.Fail(DomainErrorCode.NoPendingDecision, "no-pending-decision");

    public Result<CareerState> ResolveDecision(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)
        => Result<CareerState>.Fail(DomainErrorCode.NoPendingDecision, "no-pending-decision");

    /// <summary>M1 actions are never rolled.</summary>
    public ChoiceOdds? PresentAction(CareerState s, ContentSnapshot c, WeekPlan plan) => null;

    /// <summary>M1 has no sponsors, so there is never an offer to answer.</summary>
    public Result<CareerState> RespondOffer(CareerState s, ContentSnapshot c, string offerId, bool accept, long expectedVersion)
        => Result<CareerState>.Fail(DomainErrorCode.IllegalAction, "unknown-offer");

    /// <summary>M1 plans risk nothing beyond what the plan screen already shows.</summary>
    public PlanPreview PresentPlan(CareerState s, ContentSnapshot c, WeekPlan plan) => PlanPreview.None;
}
