using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
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
    public void Viral_flag_is_active_for_exactly_the_next_four_weeks()
    {
        var (state, report) = M3Play.UntilViral();
        var active = Enumerable.Range(0, 8)
            .Select(offset => CareerFlags.IsActive(state with { Week = new CareerWeek(report.Week.Value + 1 + offset) }, "viral-clip"))
            .ToArray();
        Assert.Equal([true, true, true, true, false, false, false, false], active);
    }

    [Theory]
    [InlineData("viral-clip", false)]
    [InlineData("sys:rest-only", false)]
    [InlineData("ghost", true)]
    public void Engine_flags_are_known_to_the_unset_flag_check(string flag, bool reported)
    {
        var c = M3Play.C;
        var first = c.Events[0];
        var required = first with { Id = "needs-flag", Eligibility = first.Eligibility with { RequiresFlags = [flag] } };
        var violations = ContentValidator.Validate(c with { Events = c.Events.Add(required) });
        Assert.Equal(reported, violations.Contains($"unset-flag:needs-flag/{flag}"));
    }

    [Fact]
    public void Negative_event_effects_cannot_push_the_chance_below_zero()
        => Assert.Equal(Fixed.Zero, M3Ruleset.ViralChance(M3Play.C.Balance, Category.Chess, Fixed.Zero, 0, Fixed.FromInt(-50)));
}
