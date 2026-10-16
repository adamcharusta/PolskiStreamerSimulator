using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Careers;

public static class CareerFactory
{
    /// <summary>PCG32 stream used for career simulation draws.</summary>
    public const ulong CareerStream = 0xDA3E39CB94B95BDB;

    public static CareerState Create(
        string rulesetId,
        string contentDigest,
        StartingProfile profile,
        Background background,
        Difficulty difficulty,
        PronounSet pronouns,
        ulong seed) => new()
        {
            RulesetId = rulesetId,
            ContentDigest = contentDigest,
            StateVersion = 0,
            Background = background,
            Difficulty = difficulty,
            Pronouns = pronouns,
            Week = new CareerWeek(CareerWeek.First),
            Rng = Pcg32.Create(seed, CareerStream),
            CashPln = profile.CashPln,
            Followers = profile.Followers,
            LastLiveViewers = 0,
            Drama = profile.Drama,
            Credibility = profile.Credibility,
            Energy = profile.Energy,
            Burnout = Fixed.Zero,
            Skill = profile.Skill,
            Boredom = CategoryValues<Fixed>.Uniform(Fixed.Zero),
            InactiveWeeks = CategoryValues<int>.Uniform(0),
            Equipment = profile.Equipment,
            EventCooldowns = ImmutableDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal),
            Achievements = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal),
            Metrics = CareerMetrics.Empty,
            Status = CareerStatus.Active,
        };
}
