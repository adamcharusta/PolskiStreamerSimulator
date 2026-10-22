using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class EventSelectorTests
{
    /// <summary>Balance without a policy section, so strikes stay active for the default 26 weeks.</summary>
    private static readonly BalanceConstants B = TestContent.Valid().Balance;

    private static EventDefinition Live(Func<Eligibility, Eligibility> edit)
    {
        var e = M3TestContent.Decision("x", EventPhase.Live, 1, CareerWeek.Last, oneTime: false);
        return e with { Eligibility = edit(e.Eligibility) };
    }

    /// <summary>A career that has settled <paramref name="settledWeek"/> with one strike from Czatnia in week 1.</summary>
    private static CareerState Struck(int settledWeek, Platform platform = Platform.Czatnia)
        => TestCareers.Fresh() with
        {
            Week = new CareerWeek(settledWeek + 1),
            LastSettledWeek = settledWeek,
            Platform = platform,
            Strikes = [new PolicyStrike(Platform.Czatnia, 1)],
        };

    [Fact]
    public void Platform_eligibility_follows_the_current_platform()
    {
        var e = Live(x => x with { Platforms = [Platform.Nurt] });
        var s = TestCareers.Fresh();
        Assert.False(EventSelector.IsEligible(s, B, e, Category.Fps));
        Assert.False(EventSelector.IsEligible(s with { Platform = Platform.PulsLive }, B, e, Category.Fps));
        Assert.True(EventSelector.IsEligible(s with { Platform = Platform.Nurt }, B, e, Category.Fps));
        Assert.True(EventSelector.IsEligible(s with { Platform = Platform.Nurt }, B, e, category: null));
    }

    [Fact]
    public void Events_without_platforms_are_eligible_everywhere()
    {
        var e = Live(x => x);
        Assert.All(Enum.GetValues<Platform>(), p => Assert.True(EventSelector.IsEligible(TestCareers.Fresh() with { Platform = p }, B, e, Category.Fps)));
    }

    [Fact]
    public void Min_strikes_counts_only_active_strikes_on_the_current_platform()
    {
        var e = Live(x => x with { MinStrikes = 1 });
        Assert.False(EventSelector.IsEligible(TestCareers.Fresh(), B, e, Category.Fps));
        Assert.True(EventSelector.IsEligible(Struck(settledWeek: 26), B, e, Category.Fps));
        Assert.False(EventSelector.IsEligible(Struck(settledWeek: 27), B, e, Category.Fps));
        Assert.False(EventSelector.IsEligible(Struck(settledWeek: 26, Platform.PulsLive), B, e, Category.Fps));
    }

    [Fact]
    public void Min_strikes_uses_the_strike_weeks_of_the_balance()
    {
        var e = Live(x => x with { MinStrikes = 1 });
        var longer = B with { Policy = new PolicyBalance(1, 1, 1, Fixed.FromInt(70), Fixed.FromRatio(15, 1000), Fixed.FromRatio(15, 100), 30, 3, Fixed.One) };
        Assert.True(EventSelector.IsEligible(Struck(settledWeek: 30), longer, e, Category.Fps));
        Assert.False(EventSelector.IsEligible(Struck(settledWeek: 31), longer, e, Category.Fps));
        Assert.False(EventSelector.IsEligible(Struck(settledWeek: 30), B, e, Category.Fps));
    }

    [Fact]
    public void Active_strikes_count_strikes_on_one_platform_inside_the_strike_window()
    {
        var s = TestCareers.Fresh() with
        {
            Strikes = [new PolicyStrike(Platform.Czatnia, 1), new PolicyStrike(Platform.Czatnia, 10), new PolicyStrike(Platform.Nurt, 10)],
        };
        Assert.Equal(2, Economy.ActiveStrikes(s, Platform.Czatnia, settledWeek: 26, strikeWeeks: 26));
        Assert.Equal(1, Economy.ActiveStrikes(s, Platform.Czatnia, settledWeek: 27, strikeWeeks: 26));
        Assert.Equal(0, Economy.ActiveStrikes(s, Platform.Czatnia, settledWeek: 36, strikeWeeks: 26));
        Assert.Equal(1, Economy.ActiveStrikes(s, Platform.Nurt, settledWeek: 26, strikeWeeks: 26));
        Assert.Equal(0, Economy.ActiveStrikes(s, Platform.PulsLive, settledWeek: 26, strikeWeeks: 26));
        Assert.Equal(0, Economy.ActiveStrikes(TestCareers.Fresh(), Platform.Czatnia, settledWeek: 26, strikeWeeks: 26));
    }
}
