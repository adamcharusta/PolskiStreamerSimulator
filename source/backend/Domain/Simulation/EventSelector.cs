using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

public static class EventSelector
{
    /// <summary>
    /// Draws the week's event count, then picks that many distinct live events by weight among those eligible
    /// for the week and stats and not cooling down. Cumulative weights run in content order.
    /// </summary>
    public static ImmutableArray<string> Select(CareerState s, ContentSnapshot c, Category primary, ref Pcg32 rng)
    {
        (var count, rng) = rng.NextInt(c.Balance.EventsPerWeekMin, c.Balance.EventsPerWeekMax);
        var selected = ImmutableArray.CreateBuilder<string>(count);
        Draw(LiveCandidates(s, c, primary), count, selected, ref rng);
        return selected.ToImmutable();
    }

    /// <summary>
    /// M3 selection: draws the week's event count, fills the first slots with due live follow-ups from
    /// <paramref name="scheduled"/> (in scheduling order, skipping those still cooling down), then draws the rest like
    /// <see cref="Select(CareerState, ContentSnapshot, Category, ref Pcg32)"/>. Returns the events and the follow-ups
    /// still waiting.
    /// </summary>
    public static (ImmutableArray<string> EventIds, ImmutableArray<ScheduledEvent> Scheduled) Select(
        CareerState s, ContentSnapshot c, Category primary, ImmutableArray<ScheduledEvent> scheduled, ref Pcg32 rng)
    {
        (var count, rng) = rng.NextInt(c.Balance.EventsPerWeekMin, c.Balance.EventsPerWeekMax);
        var selected = ImmutableArray.CreateBuilder<string>(count);
        var waiting = scheduled.ToBuilder();
        var due = scheduled
            .Where(x => x.DueWeek <= s.Week.Value && IsLive(c, x.EventId) && !IsCoolingDown(s, x.EventId))
            .DistinctBy(x => x.EventId, StringComparer.Ordinal)
            .Take(count);
        foreach (var eventId in due.Select(followUp => followUp.EventId))
        {
            selected.Add(eventId);
            waiting.RemoveAll(x => x.EventId == eventId && x.DueWeek <= s.Week.Value);
        }

        var candidates = LiveCandidates(s, c, primary);
        candidates.RemoveAll(e => selected.Contains(e.Id));
        Draw(candidates, count, selected, ref rng);
        return (selected.ToImmutable(), waiting.ToImmutable());
    }

    /// <summary>
    /// Whether <paramref name="e"/> may be shown now: inside its weeks, not cooling down, within its stat bounds, its
    /// required flags active and forbidden flags inactive, not an already used one-time event and — when
    /// <paramref name="category"/> is given — allowed for that category. Stat bounds on the primary category read
    /// <paramref name="category"/>, or the last streamed category for decisions.
    /// </summary>
    public static bool IsEligible(CareerState s, EventDefinition e, Category? category)
    {
        var week = s.Week.Value;
        var eligibility = e.Eligibility;
        var primary = category ?? Stats.LastPrimary(s);
        return eligibility.FromWeek <= week
            && week <= eligibility.ToWeek
            && !IsCoolingDown(s, e.Id)
            && eligibility.Min.All(bound => Stats.Get(s, bound.Key, primary) >= bound.Value)
            && eligibility.Max.All(bound => Stats.Get(s, bound.Key, primary) <= bound.Value)
            && (category is null || eligibility.Categories.IsEmpty || eligibility.Categories.Contains(category.Value))
            && eligibility.RequiresFlags.All(flag => CareerFlags.IsActive(s, flag))
            && !eligibility.ForbidsFlags.Any(flag => CareerFlags.IsActive(s, flag))
            && !(e.OneTime && s.UsedOneTime is { } used && used.Contains(e.Id));
    }

    /// <summary>The candidate holding <paramref name="ticket"/> (1 to the weight sum) by cumulative weight in list order.</summary>
    public static EventDefinition PickByWeight(IReadOnlyList<EventDefinition> candidates, int ticket)
        => candidates[PickIndex(candidates, ticket)];

    private static List<EventDefinition> LiveCandidates(CareerState s, ContentSnapshot c, Category primary)
        => [.. c.Events.Where(e => e.Phase == EventPhase.Live && IsEligible(s, e, primary))];

    private static void Draw(List<EventDefinition> candidates, int count, ImmutableArray<string>.Builder selected, ref Pcg32 rng)
    {
        while (selected.Count < count && candidates.Count > 0)
        {
            (var ticket, rng) = rng.NextInt(1, candidates.Sum(e => e.Weight));
            var index = PickIndex(candidates, ticket);
            selected.Add(candidates[index].Id);
            candidates.RemoveAt(index);
        }
    }

    private static int PickIndex(IReadOnlyList<EventDefinition> candidates, int ticket)
    {
        var index = 0;
        for (var cumulative = candidates[0].Weight; cumulative < ticket; cumulative += candidates[index].Weight)
        {
            index++;
        }

        return index;
    }

    private static bool IsCoolingDown(CareerState s, string eventId)
        => s.EventCooldowns.TryGetValue(eventId, out var availableFrom) && s.Week.Value < availableFrom;

    private static bool IsLive(ContentSnapshot c, string eventId)
        => c.Events.FirstOrDefault(e => e.Id == eventId) is { Phase: EventPhase.Live };
}
