using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets;

/// <summary>Versioned rule implementation; a career always uses the ruleset it started with.</summary>
public interface IRuleset
{
    string Id { get; }

    /// <summary>Validates and applies the main action, then selects the week's live events.</summary>
    Result<CareerState> StartWeek(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion);

    /// <summary>Every plan for the next week, with the reason it is disabled or <c>null</c> when legal.</summary>
    IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c);

    /// <summary>Odds of the next unresolved event; never draws a random value.</summary>
    Result<EventPresentation> PresentEvent(CareerState s, ContentSnapshot c);

    /// <summary>Rolls (unless guaranteed) and records the chosen response to the next unresolved event.</summary>
    Result<CareerState> ResolveEvent(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion);

    /// <summary>Settles a week whose events are all resolved, produces its report and advances the week once.</summary>
    Result<(CareerState State, WeekReport Report)> SettleWeek(CareerState s, ContentSnapshot c, long expectedVersion);

    /// <summary>Ends the career between weeks with <c>EarlyRetirement</c>.</summary>
    Result<CareerState> Retire(CareerState s, ContentSnapshot c, long expectedVersion);

    /// <summary>
    /// Whether the next week needs a player decision before it may start; an automated batch stops here without
    /// starting the week.
    /// </summary>
    bool RequiresPlayerDecision(CareerState s, ContentSnapshot c);

    /// <summary>Odds of the first queued mandatory decision; fails with <c>NoPendingDecision</c> when none is queued.</summary>
    Result<EventPresentation> PresentDecision(CareerState s, ContentSnapshot c);

    /// <summary>
    /// Answers the first queued mandatory decision between weeks: stat effects apply now, cash, follower effects and
    /// the roll are carried into the next week's report.
    /// </summary>
    Result<CareerState> ResolveDecision(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion);

    /// <summary>Odds of the main action's roll for <paramref name="plan"/>, or <c>null</c> when the action is not rolled.</summary>
    ChoiceOdds? PresentAction(CareerState s, ContentSnapshot c, WeekPlan plan);

    /// <summary>
    /// Accepts or declines an open sponsor offer between weeks; a free command that draws nothing. Fails with
    /// <c>unknown-offer</c> when the career has no such offer.
    /// </summary>
    Result<CareerState> RespondOffer(CareerState s, ContentSnapshot c, string offerId, bool accept, long expectedVersion);
}
