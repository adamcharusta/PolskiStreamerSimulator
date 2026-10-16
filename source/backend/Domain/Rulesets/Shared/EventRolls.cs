using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.Shared;

/// <summary>A rolled answer to an event: the recorded roll, the effects to apply and the advanced RNG.</summary>
public sealed record EventAnswer(EventDefinition Event, RollResult Roll, ImmutableArray<Effect> Effects, Pcg32 Rng);

/// <summary>Answering an event (live or decision) with one of its choices, shared by the M1 and M3 rulesets.</summary>
public static class EventRolls
{
    /// <summary>Rolls <paramref name="choiceId"/> of <paramref name="e"/> with the odds shown to the player.</summary>
    public static Result<EventAnswer> Answer(CareerState s, ContentSnapshot c, EventDefinition e, string choiceId)
    {
        if (e.Choices.FirstOrDefault(choice => choice.Id == choiceId) is not { } chosen)
        {
            return Result<EventAnswer>.Fail(DomainErrorCode.UnknownChoice, choiceId);
        }

        var odds = OddsCalculator.For(s, chosen, c.Balance);
        var rng = s.Rng;
        var (roll, outcome, effects) = RuleSteps.Roll(chosen, odds, ref rng);
        var result = new RollResult(e.Id, chosen.Id, roll, odds.Threshold, odds.Modifiers, outcome);
        return Result<EventAnswer>.Ok(new EventAnswer(e, result, effects, rng));
    }

    /// <summary>
    /// Records an applied answer on <paramref name="state"/>: the advanced RNG, the next state version, the event's
    /// cooldown from <paramref name="before"/>'s week and the resolved-event metrics.
    /// </summary>
    public static CareerState Recorded(CareerState before, CareerState state, EventAnswer answer)
    {
        var critical = answer.Roll.Outcome is RollOutcome.CriticalSuccess or RollOutcome.CriticalFailure;
        return state with
        {
            Rng = answer.Rng,
            StateVersion = before.StateVersion + 1,
            EventCooldowns = before.EventCooldowns.SetItem(answer.Event.Id, before.Week.Value + answer.Event.CooldownWeeks),
            Metrics = before.Metrics with
            {
                EventsResolved = before.Metrics.EventsResolved + 1,
                Criticals = before.Metrics.Criticals + (critical ? 1 : 0),
            },
        };
    }
}
