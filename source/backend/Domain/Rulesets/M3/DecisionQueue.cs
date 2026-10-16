using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3;

/// <summary>
/// Opens the mandatory decisions before a week, in order: due decision follow-ups, the recovery crisis when energy
/// is 0 or burnout 100, the first eligible one-time milestone, then — only if nothing opened — one repeatable
/// milestone or crisis after a seeded <see cref="BalanceConstants.DecisionChancePerWeek"/> roll. At most two open.
/// </summary>
public static class DecisionQueue
{
    public const int MaxPerWeek = 2;

    public static CareerState Open(CareerState s, ContentSnapshot c, ref Pcg32 rng)
    {
        var week = s.Week.Value;
        var queue = ImmutableArray.CreateBuilder<string>();
        var scheduled = (s.Scheduled ?? []).ToList();
        foreach (var due in scheduled.Where(x => x.DueWeek <= week && IsDecision(c, x.EventId)).Take(MaxPerWeek).ToList())
        {
            queue.Add(due.EventId);
            scheduled.Remove(due);
        }

        if (c.Balance.RecoveryCrisisEventId is { } recovery
            && (s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred)
            && queue.Count < MaxPerWeek && !queue.Contains(recovery))
        {
            queue.Add(recovery);
        }

        var candidates = c.Events
            .Where(e => e.Phase != EventPhase.Live && e.Id != c.Balance.RecoveryCrisisEventId && !queue.Contains(e.Id))
            .Where(e => EventSelector.IsEligible(s, e, category: null))
            .ToList();
        if (queue.Count < MaxPerWeek && candidates.FirstOrDefault(e => e.OneTime) is { } milestone)
        {
            queue.Add(milestone.Id);
        }
        else if (queue.Count == 0 && c.Balance.DecisionChancePerWeek > Fixed.Zero && candidates.Any(e => !e.OneTime))
        {
            (var draw, rng) = rng.NextInt(1, 100);
            if (Fixed.FromInt(draw) <= c.Balance.DecisionChancePerWeek * Fixed.Hundred)
            {
                var repeatable = candidates.Where(e => !e.OneTime).ToList();
                (var ticket, rng) = rng.NextInt(1, repeatable.Sum(e => e.Weight));
                queue.Add(EventSelector.PickByWeight(repeatable, ticket).Id);
            }
        }

        return s with
        {
            PendingDecisions = queue.ToImmutable(),
            Scheduled = [.. scheduled],
        };
    }

    private static bool IsDecision(ContentSnapshot c, string id)
        => c.Events.FirstOrDefault(e => e.Id == id) is { Phase: not EventPhase.Live };
}
