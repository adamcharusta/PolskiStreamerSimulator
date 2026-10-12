using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>Reads and changes bounded (0–100) stats by <see cref="StatKey"/>.</summary>
public static class Stats
{
    public static Fixed Bounded(Fixed value) => value.Clamp(Fixed.Zero, Fixed.Hundred);

    public static Fixed Get(CareerState s, StatKey key, Category primary) => key switch
    {
        StatKey.Drama => s.Drama,
        StatKey.Credibility => s.Credibility,
        StatKey.Energy => s.Energy,
        StatKey.Burnout => s.Burnout,
        StatKey.PrimarySkill => s.Skill[primary],
        StatKey.PrimaryBoredom => s.Boredom[primary],
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };

    /// <summary>Adds <paramref name="delta"/> and clamps the result to 0–100.</summary>
    public static CareerState Add(CareerState s, StatKey key, Fixed delta, Category primary) => key switch
    {
        StatKey.Drama => s with { Drama = Bounded(s.Drama + delta) },
        StatKey.Credibility => s with { Credibility = Bounded(s.Credibility + delta) },
        StatKey.Energy => s with { Energy = Bounded(s.Energy + delta) },
        StatKey.Burnout => s with { Burnout = Bounded(s.Burnout + delta) },
        StatKey.PrimarySkill => s with { Skill = s.Skill.With(primary, Bounded(s.Skill[primary] + delta)) },
        StatKey.PrimaryBoredom => s with { Boredom = s.Boredom.With(primary, Bounded(s.Boredom[primary] + delta)) },
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };
}
