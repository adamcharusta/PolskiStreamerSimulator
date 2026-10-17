using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.Shared;

/// <summary>Presenting and resolving the live events of a pending week, shared by the M1 and M3 rulesets.</summary>
public static class LiveEvents
{
    public static Result<EventPresentation> Present(CareerState s, ContentSnapshot c) => Present(s, c, Next(s, c));

    /// <summary>
    /// The odds of every choice of the looked-up <paramref name="next"/> event (live or decision); fails with
    /// <c>CareerEnded</c> on an ended career and with the lookup error otherwise. Never draws a random value.
    /// </summary>
    public static Result<EventPresentation> Present(CareerState s, ContentSnapshot c, Result<EventDefinition> next)
    {
        if (s.Status == CareerStatus.Ended)
        {
            return Result<EventPresentation>.Fail(DomainErrorCode.CareerEnded, "career-ended");
        }

        return next.IsSuccess
            ? Result<EventPresentation>.Ok(new EventPresentation(next.Value.Id, [.. next.Value.Choices.Select(choice => OddsCalculator.For(s, choice, c.Balance))]))
            : Result<EventPresentation>.Fail(next.Error);
    }

    /// <summary>
    /// Rolls the chosen response to the next unresolved event, applies its effects to the career and the pending
    /// week and records the roll; also returns the answered event.
    /// </summary>
    public static Result<(CareerState State, EventDefinition Event)> Resolve(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)
    {
        if (RuleSteps.CommandRejection(s, expectedVersion) is { } error)
        {
            return Result<(CareerState, EventDefinition)>.Fail(error);
        }

        var next = Next(s, c);
        var answer = next.IsSuccess ? EventRolls.Answer(s, c, next.Value, choiceId) : Result<EventAnswer>.Fail(next.Error);
        if (!answer.IsSuccess)
        {
            return Result<(CareerState, EventDefinition)>.Fail(answer.Error);
        }

        var pending = s.Pending!;
        var state = EffectApplier.ToWeek(s, next.Value.Id, answer.Value.Effects, ref pending);
        state = EventRolls.Recorded(s, state, answer.Value) with
        {
            Pending = pending with { Resolved = pending.Resolved.Add(answer.Value.Roll) },
        };
        return Result<(CareerState, EventDefinition)>.Ok((state, next.Value));
    }

    private static Result<EventDefinition> Next(CareerState s, ContentSnapshot c)
    {
        if (s.Pending is not { } pending)
        {
            return Result<EventDefinition>.Fail(DomainErrorCode.NoPendingWeek, "no-pending-week");
        }

        if (pending.Resolved.Length >= pending.EventIds.Length)
        {
            return Result<EventDefinition>.Fail(DomainErrorCode.IllegalAction, "all-resolved");
        }

        return RuleSteps.FindEvent(c, pending.EventIds[pending.Resolved.Length]);
    }
}
