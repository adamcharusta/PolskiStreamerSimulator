using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Settlement;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class AudienceRulesTests
{
    private static readonly BalanceConstants Balance = TestWorld.Content.Balance;
    private static readonly BalanceConstants NoNoise = Balance with { NoiseMin = Fixed.One, NoiseMax = Fixed.One };

    private static PendingWeek Pending(Fixed? multiplier = null, Fixed? eventQuality = null)
        => new(new CareerWeek(1), new WeekPlan(MainAction.NoAction, Category.Fps), [], [], eventQuality ?? Fixed.Zero, multiplier ?? Fixed.One, 0, []);

    private static CareerState Streamer(long followers)
        => TestCareers.Dorm(1) with { Followers = followers, Energy = Fixed.FromInt(67), Burnout = Fixed.FromInt(3) };

    private static AudienceResult Compute(CareerState s, PendingWeek p, BalanceConstants b)
    {
        var rng = s.Rng;
        return AudienceRules.Compute(s, p, ref rng, b);
    }

    [Fact]
    public void Formula_matches_a_hand_computed_week()
    {
        // quality = 25 + 0.35×18 + 4×0 + 0 − 0.20×3 + 0.10×67 = 37.4
        // baseline = (50 + 1.5×18 + 35×√10000 + 0.025×10000) × 1.0 × (0.6 + 0.374) = 3827 × 0.974 = 3727.498
        // live = round(3727.498 × (1 − 0/250) × 1 × 1) = 3727
        // gained = round(3727 × 0.05 × 50/50) = 186; lost = round(10000 × (0.004 + 0.0001×0)) = 40
        var r = Compute(Streamer(10_000), Pending(), NoNoise);
        Assert.Equal(Fixed.FromRatio(374, 10), r.Quality);
        Assert.Equal(3_727, r.LiveViewers);
        Assert.Equal(186, r.Gained);
        Assert.Equal(40, r.Lost);
        Assert.Equal(146, r.Net);
    }

    [Fact]
    public void Boredom_credibility_and_event_quality_shape_the_audience()
    {
        var s = Streamer(10_000) with { Credibility = Fixed.FromInt(100) };
        s = s with { Boredom = s.Boredom.With(Category.Fps, Fixed.FromInt(50)) };
        // quality 37.4 + 10 = 47.4; baseline = 3827 × 1.074 = 4110.198; live = round(4110.198 × 0.8) = 3288
        // gained = round(3288 × 0.05 × 2) = 329; lost = round(10000 × (0.004 + 0.005)) = 90
        var r = Compute(s, Pending(eventQuality: Fixed.FromInt(10)), NoNoise);
        Assert.Equal(3_288, r.LiveViewers);
        Assert.Equal(329, r.Gained);
        Assert.Equal(90, r.Lost);
    }

    [Fact]
    public void Quality_is_clamped_to_100()
        => Assert.Equal(Fixed.Hundred, Compute(Streamer(0), Pending(eventQuality: Fixed.FromInt(500)), NoNoise).Quality);

    [Fact]
    public void Zero_followers_still_get_minimum_viewers()
    {
        var s = Streamer(0);
        s = s with { Skill = s.Skill.With(Category.Fps, Fixed.Zero), Energy = Fixed.Zero, Burnout = Fixed.Hundred };
        var tiny = Balance with { ViewersBase = Fixed.One };
        // baseline = max(5, 1 × (0.6 + 0.05)) = 5; live ≥ round(5 × 0.85)
        var r = Compute(s, Pending(), tiny);
        Assert.InRange(r.LiveViewers, 4, 6);
        Assert.Equal(0, r.Lost);
    }

    [Fact]
    public void Fifty_million_followers_do_not_overflow()
    {
        var r = Compute(Streamer(50_000_000), Pending(Fixed.FromInt(3)), Balance);
        Assert.True(r.LiveViewers > 1_000_000);
        Assert.True(r.Lost > 0);
        Assert.Equal(r.Gained - r.Lost, r.Net);
    }

    [Fact]
    public void Viral_cap_limits_viewers_to_three_times_baseline()
    {
        var capped = Compute(Streamer(10_000), Pending(Fixed.FromInt(10)), NoNoise).LiveViewers;
        Assert.Equal(capped, Compute(Streamer(10_000), Pending(Fixed.FromInt(100)), NoNoise).LiveViewers);
        Assert.Equal(11_182, capped); // round(3 × 3727.498)
    }

    [Fact]
    public void Followers_and_viewers_are_reported_separately()
    {
        var r = Compute(Streamer(10_000), Pending(), Balance);
        Assert.Equal(r.Gained - r.Lost, r.Net);
        Assert.NotEqual(r.LiveViewers, r.Net);
        Assert.InRange(r.LiveViewers, 3_168, 4_287); // 3727.498 × 0.85..1.15
    }

    [Fact]
    public void Noise_comes_from_the_career_generator()
    {
        var a = Compute(Streamer(10_000) with { Rng = Pcg32.Create(1, 1) }, Pending(), Balance).LiveViewers;
        var b = Compute(Streamer(10_000) with { Rng = Pcg32.Create(1, 1) }, Pending(), Balance).LiveViewers;
        Assert.Equal(a, b);
        var values = Enumerable.Range(1, 20).Select(i => Compute(Streamer(10_000) with { Rng = Pcg32.Create((ulong)i, 1) }, Pending(), Balance).LiveViewers);
        Assert.True(values.Distinct().Count() > 1);
    }
}
