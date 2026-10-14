using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Domain.Simulation.Policies;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>Outcome of one automated career; <see cref="InvariantViolations"/> is empty for a valid run.</summary>
public sealed record CareerRunResult(CareerState Final, int WeeksPlayed, IReadOnlyList<string> InvariantViolations, string FinalHash);

public static class CareerRunner
{
    /// <summary>
    /// Plays a career to its ending through the engine (start, present and resolve every event, settle),
    /// checking invariants after every step and stopping at the first violation or rejected command.
    /// </summary>
    public static CareerRunResult Run(
        WeekEngine engine,
        ContentSnapshot c,
        CareerState start,
        IChoicePolicy policy,
        Action<CareerState, WeekReport>? onWeek = null)
    {
        var s = start;
        var weeks = 0;
        var violations = new List<string>();
        Check(s, "start", violations);
        while (violations.Count == 0 && s.Status == CareerStatus.Active)
        {
            var week = $"week {s.Week.Value}";
            if (policy.ShouldRetire(s))
            {
                Retire(engine, c, week, violations, ref s);
                break;
            }

            var legal = engine.LegalPlans(s, c);
            if (!legal.IsSuccess)
            {
                violations.Add($"{week} legal-plans: {legal.Error.Code} ({legal.Error.Detail})");
                break;
            }

            if (!Accept(engine.StartWeek(s, c, policy.ChoosePlan(s, c, legal.Value), s.StateVersion), $"{week} start", violations, ref s)
                || !Check(s, $"{week} start", violations)
                || !ResolveEvents(engine, c, policy, week, violations, ref s))
            {
                break;
            }

            var settled = engine.SettleWeek(s, c, s.StateVersion);
            if (!settled.IsSuccess)
            {
                violations.Add($"{week} settle: {settled.Error.Code} ({settled.Error.Detail})");
                break;
            }

            s = settled.Value.State;
            weeks++;
            if (!Check(s, $"{week} settle", violations))
            {
                break;
            }

            onWeek?.Invoke(s, settled.Value.Report);
        }

        return new CareerRunResult(s, weeks, violations, StateHash.Of(s));
    }

    private static void Retire(WeekEngine engine, ContentSnapshot c, string week, List<string> violations, ref CareerState s)
    {
        if (Accept(engine.Retire(s, c, s.StateVersion), $"{week} retire", violations, ref s))
        {
            Check(s, $"{week} retire", violations);
        }
    }

    private static bool ResolveEvents(WeekEngine engine, ContentSnapshot c, IChoicePolicy policy, string week, List<string> violations, ref CareerState s)
    {
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            var presentation = engine.PresentEvent(s, c);
            if (!presentation.IsSuccess)
            {
                violations.Add($"{week} present: {presentation.Error.Code} ({presentation.Error.Detail})");
                return false;
            }

            var step = $"{week} resolve {presentation.Value.EventId}";
            var choice = policy.ChooseEventChoice(s, presentation.Value);
            if (!Accept(engine.ResolveEvent(s, c, choice, s.StateVersion), step, violations, ref s) || !Check(s, step, violations))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Accept(Result<CareerState> result, string step, List<string> violations, ref CareerState s)
    {
        if (!result.IsSuccess)
        {
            violations.Add($"{step}: {result.Error.Code} ({result.Error.Detail})");
            return false;
        }

        s = result.Value;
        return true;
    }

    private static bool Check(CareerState s, string step, List<string> violations)
    {
        var found = CareerInvariants.Validate(s);
        violations.AddRange(found.Select(code => $"{step}: {code}"));
        return found.Count == 0;
    }
}
