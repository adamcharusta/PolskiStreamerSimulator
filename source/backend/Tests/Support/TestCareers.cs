using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Tests.Support;

public static class TestCareers
{
    public const string RulesetId = "m1-prototype.1";

    public static StartingProfile DormProfile { get; } = new(
        CashPln: 2_000,
        Followers: 0,
        Drama: Fixed.Zero,
        Credibility: Fixed.FromInt(50),
        Energy: Fixed.FromInt(70),
        Skill: CategoryValues<Fixed>.Uniform(Fixed.FromInt(10)).With(Category.Fps, Fixed.FromInt(18)),
        Equipment: Enum.GetValues<EquipmentSlot>().ToImmutableDictionary(slot => slot, _ => 0),
        ScoreMultiplier: Fixed.One);

    public static CareerState Dorm(ulong seed)
        => CareerFactory.Create(RulesetId, "test", DormProfile, Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman, seed);
}
