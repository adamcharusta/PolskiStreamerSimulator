using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Settlement;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class ViralTests
{
    private static PendingWeek Week() => new(new CareerWeek(5), new WeekPlan(MainAction.NoAction, Category.Gambling), [], [], Fixed.Zero, Fixed.One, 0, []);

    private static AudienceResult FirstViral(CareerState s)
    {
        for (ulong seed = 1; seed <= 500; seed++)
        {
            var x = s with { Rng = Pcg32.Create(seed, CareerFactory.CareerStream) };
            var rng = x.Rng;
            var result = AudienceRules.Compute(x, Week(), ref rng, M3Play.C.Balance, Fixed.FromRatio(30, 100));
            if (result.Viral is not null)
            {
                return result;
            }
        }

        throw new InvalidOperationException("no viral");
    }

    [Fact]
    public void Zero_chance_never_draws()
    {
        var s = TestCareers.Fresh() with { Followers = 5_000 };
        var withChance = s.Rng;
        var without = s.Rng;
        _ = AudienceRules.Compute(s, Week(), ref withChance, TestWorld.Content.Balance, Fixed.Zero);
        _ = AudienceRules.Compute(s, Week(), ref without, TestWorld.Content.Balance);
        Assert.Equal(without, withChance);
    }

    [Fact]
    public void Viral_multiplier_and_follower_bonus_are_bounded()
    {
        var b = M3Play.C.Balance;
        var viral = FirstViral(TestCareers.Fresh() with { Followers = 50_000 }).Viral!;
        Assert.True(viral.Multiplier >= b.ViralMultiplierMin && viral.Multiplier <= b.ViralMultiplierMax);
        Assert.InRange(viral.FollowersBonus, b.ViralFollowerMin, 5_000);
    }

    [Fact]
    public void Small_career_gets_the_minimum_bonus()
        => Assert.Equal(100, FirstViral(TestCareers.Fresh() with { Followers = 0 }).Viral!.FollowersBonus);

    [Fact]
    public void Chance_combines_its_parts_and_is_capped()
    {
        var b = M3Play.C.Balance;
        Assert.Equal(b.ViralChanceCap, M3Ruleset.ViralChance(b, Category.Gambling, Fixed.Hundred, 5, Fixed.FromInt(30)));
        Assert.Equal(b.Profile(Category.Chess).ViralBase, M3Ruleset.ViralChance(b, Category.Chess, Fixed.FromInt(70), 0, Fixed.Zero));
        Assert.Equal(
            b.Profile(Category.Fps).ViralBase + (Fixed.FromInt(10) * b.ViralPerQualityPoint) + b.ViralPerCritical,
            M3Ruleset.ViralChance(b, Category.Fps, Fixed.FromInt(80), 1, Fixed.Zero));
    }

    [Fact]
    public void Report_keeps_followers_and_live_viewers_separate_in_a_viral_week()
    {
        var (state, report) = M3Play.UntilViral();
        Assert.Equal(report.FollowersGained - report.FollowersLost, report.FollowersNet);
        Assert.True(report.FollowersGained >= report.Viral!.FollowersBonus);
        Assert.True(CareerFlags.IsActive(state, "viral-clip"));
    }

    [Fact]
    public void Odds_show_the_viral_chance_a_critical_can_add()
    {
        var critical = M3Play.C.Events.SelectMany(e => e.Choices).First(c => c.AllowsCritical);
        Assert.Equal(M3Play.C.Balance.ViralPerCritical, OddsCalculator.For(M3Play.Fresh(), critical, M3Play.C.Balance).ViralOnCritical);
    }

    [Fact]
    public void Engine_flags_count_as_set_for_the_validator()
    {
        Assert.Contains("viral-clip", PolskiStreamerSimulator.Domain.Content.ContentValidator.EngineFlags);
    }
}
