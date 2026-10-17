using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Scoring;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.Shared;

/// <summary>Rule steps shared by the M1 and M3 rulesets.</summary>
public static class RuleSteps
{
    /// <summary>Ends the career once: read-only status, reason and the final score breakdown.</summary>
    public static CareerState End(CareerState s, EndingReason reason, ContentSnapshot c) => s with
    {
        Status = CareerStatus.Ended,
        Ending = reason,
        Pending = null,
        FinalScore = ScoreCalculator.Compute(
            s.Followers,
            s.CashPln,
            Achievements.Points(s.Achievements),
            s.Difficulty,
            c.Profiles[s.Background].ScoreMultiplier,
            reason,
            c.Balance),
    };

    /// <summary>Ends the career between weeks with <see cref="EndingReason.EarlyRetirement"/>.</summary>
    public static Result<CareerState> Retire(CareerState s, ContentSnapshot c, long expectedVersion)
    {
        if (CommandRejection(s, expectedVersion) is { } error)
        {
            return Result<CareerState>.Fail(error);
        }

        if (s.Pending is not null)
        {
            return Result<CareerState>.Fail(DomainErrorCode.IllegalAction, "week-in-progress");
        }

        var state = s with { Achievements = Achievements.Evaluate(s, completed: false), StateVersion = s.StateVersion + 1 };
        return Result<CareerState>.Ok(End(state, EndingReason.EarlyRetirement, c));
    }

    public static ImmutableDictionary<string, long> Accumulate(ImmutableDictionary<string, long> totals, IEnumerable<ReportLine> lines)
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
    public static (int? Roll, RollOutcome Outcome, ImmutableArray<Effect> Effects) Roll(ChoiceDefinition choice, ChoiceOdds odds, ref Pcg32 rng)
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

    public static DomainError? CommandRejection(CareerState s, long expectedVersion)
    {
        if (s.Status == CareerStatus.Ended)
        {
            return new DomainError(DomainErrorCode.CareerEnded, "career-ended");
        }

        return expectedVersion != s.StateVersion
            ? new DomainError(DomainErrorCode.StaleVersion, $"expected-{expectedVersion}-current-{s.StateVersion}")
            : null;
    }

    public static Result<EventDefinition> FindEvent(ContentSnapshot c, string id)
        => c.Events.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal)) is { } definition
            ? Result<EventDefinition>.Ok(definition)
            : Result<EventDefinition>.Fail(DomainErrorCode.ContentMismatch, $"missing-event-{id}");
}
