using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Careers;

public class CareerFactoryTests
{
    [Fact]
    public void Factory_creates_week_one_active_career()
    {
        var s = TestCareers.Dorm(seed: 7);
        Assert.Equal(1, s.Week.Value);
        Assert.Equal(CareerStatus.Active, s.Status);
        Assert.Equal(0, s.StateVersion);
        Assert.Empty(CareerInvariants.Validate(s));
    }

    [Fact]
    public void Factory_copies_the_starting_profile_and_resets_pressure()
    {
        var s = TestCareers.Dorm(seed: 7);
        Assert.Equal(2_000, s.CashPln);
        Assert.Equal(0, s.Followers);
        Assert.Equal(Fixed.FromInt(50), s.Credibility);
        Assert.Equal(Fixed.FromInt(70), s.Energy);
        Assert.Equal(Fixed.Zero, s.Burnout);
        Assert.Equal(Fixed.FromInt(18), s.Skill[Category.Fps]);
        Assert.Equal(Fixed.FromInt(10), s.Skill[Category.Chess]);
        Assert.All(Enum.GetValues<Category>(), c => Assert.Equal(Fixed.Zero, s.Boredom[c]));
        Assert.Equal(0, s.Equipment[EquipmentSlot.Camera]);
        Assert.Equal("m1-prototype.1", s.RulesetId);
        Assert.Equal("test", s.ContentDigest);
        Assert.Null(s.Ending);
        Assert.Null(s.FinalScore);
        Assert.Null(s.Pending);
    }

    [Fact]
    public void Factory_seeds_the_career_stream()
        => Assert.Equal(Pcg32.Create(7, 0xDA3E39CB94B95BDB), TestCareers.Dorm(seed: 7).Rng);

    [Fact]
    public void Different_seeds_give_different_generators()
        => Assert.NotEqual(TestCareers.Dorm(1).Rng, TestCareers.Dorm(2).Rng);
}
