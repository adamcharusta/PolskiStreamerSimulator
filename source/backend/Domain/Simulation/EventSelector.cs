using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

public static class EventSelector
{
    /// <summary>
    /// Draws the week's event count, then picks that many distinct events by weight among those eligible
    /// for the week and stats and not cooling down. Cumulative weights run in content order.
    /// </summary>
    public static ImmutableArray<string> Select(CareerState s, ContentSnapshot c, Category primary, ref Pcg32 rng)
    {
        (var count, rng) = rng.NextInt(c.Balance.EventsPerWeekMin, c.Balance.EventsPerWeekMax);
        var candidates = c.Events.Where(e => IsEligible(s, e, primary)).ToList();
        var selected = ImmutableArray.CreateBuilder<string>(count);
        while (selected.Count < count && candidates.Count > 0)
        {
            (var ticket, rng) = rng.NextInt(1, candidates.Sum(e => e.Weight));
            var index = 0;
            for (var cumulative = candidates[0].Weight; cumulative < ticket; cumulative += candidates[index].Weight)
            {
                index++;
            }

            selected.Add(candidates[index].Id);
            candidates.RemoveAt(index);
        }

        return selected.ToImmutable();
    }

    private static bool IsEligible(CareerState s, EventDefinition e, Category primary)
    {
        var week = s.Week.Value;
        var eligibility = e.Eligibility;
        return eligibility.FromWeek <= week
            && week <= eligibility.ToWeek
            && (!s.EventCooldowns.TryGetValue(e.Id, out var availableFrom) || week >= availableFrom)
            && eligibility.Min.All(bound => Stats.Get(s, bound.Key, primary) >= bound.Value)
            && eligibility.Max.All(bound => Stats.Get(s, bound.Key, primary) <= bound.Value);
    }
}
