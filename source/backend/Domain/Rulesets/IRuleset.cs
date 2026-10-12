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
}
