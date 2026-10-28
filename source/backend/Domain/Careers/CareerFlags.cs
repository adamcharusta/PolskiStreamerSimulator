using System.Collections.Immutable;

namespace PolskiStreamerSimulator.Domain.Careers;

/// <summary>Career flags: each maps to the last week in which it is active.</summary>
public static class CareerFlags
{
    public static bool IsActive(CareerState s, string flag)
        => s.Flags is { } flags && flags.TryGetValue(flag, out var last) && last >= s.Week.Value;

    /// <summary>Active from now through <c>weeks</c> weeks including the current one; permanent without <c>weeks</c>.</summary>
    public static CareerState Set(CareerState s, string flag, int? weeks)
        => s with
        {
            Flags = (s.Flags ?? ImmutableDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal))
                .SetItem(flag, weeks is { } w ? s.Week.Value + w - 1 : CareerWeek.Last),
        };

    public static CareerState Clear(CareerState s, string flag)
        => s.Flags is { } flags ? s with { Flags = flags.Remove(flag) } : s;

    /// <summary>Removes every flag whose last active week is <paramref name="settledWeek"/> or earlier.</summary>
    public static CareerState Expire(CareerState s, int settledWeek)
        => s.Flags is { } flags ? s with { Flags = flags.RemoveRange(flags.Where(f => f.Value <= settledWeek).Select(f => f.Key)) } : s;
}
