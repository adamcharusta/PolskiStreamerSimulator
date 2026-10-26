using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Rulesets.Shared;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets.M3B;

public class PolicyTests
{
    [Fact]
    public void Exposure_adds_gambling_wager_side_business_and_high_drama()
    {
        var s = M3BPlay.Fresh() with { Drama = Fixed.FromInt(70), SideBusiness = new SideBusiness(52, Fixed.FromInt(30)), CashPln = 4_000 };
        Assert.Equal(5, PolicyRules.Exposure(s, M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.Medium), M3BPlay.C));
        Assert.Equal(0, PolicyRules.Exposure(M3BPlay.Fresh(), M3BPlay.Plan(MainAction.NoAction), M3BPlay.C));
    }

    [Fact]
    public void Exposure_is_zero_without_a_policy_section()
        => Assert.Equal(0, PolicyRules.Exposure(M3Play.Fresh(), M3BPlay.Plan(MainAction.NoAction, Category.Gambling), M3Play.C));

    [Theory]
    [InlineData(3, 130, 6)]    // 3 × 1.5 % × 1.30 = 5.85 %
    [InlineData(1, 80, 1)]     // 1.2 %
    [InlineData(10, 100, 15)]  // capped
    public void Strike_chance_scales_with_exposure_and_platform_risk(int exposure, int riskHundredths, int threshold)
    {
        var chance = PolicyRules.Chance(exposure, Fixed.FromRatio(riskHundredths, 100), M3BPlay.C.Balance.Policy!);
        Assert.Equal(threshold, (int)(chance * Fixed.Hundred).RoundToLong());
    }

    [Fact]
    public void Plan_preview_shows_exposure_and_chance()
    {
        var preview = M3BPlay.Engine.PresentPlan(M3BPlay.Fresh() with { CashPln = 4_000 }, M3BPlay.C, M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.Low)).Value;
        Assert.Equal(new PolicyExposure(2, Fixed.FromRatio(3, 100), 3), preview.Policy);
    }

    [Fact]
    public void A_safe_week_draws_no_strike_roll()
    {
        var s = M3BPlay.Start(M3BPlay.Fresh(), M3BPlay.Plan(MainAction.NoAction));
        Assert.Equal(0, s.Pending!.Exposure);
        Assert.Null(M3BPlay.SettleAll(s).Report.Strike);
    }

    [Fact]
    public void Strikes_expire_after_26_weeks()
    {
        var s = M3BPlay.Fresh() with { Strikes = [new PolicyStrike(Platform.Czatnia, 1)] };
        Assert.Equal(1, PolicyRules.ActiveStrikes(s, Platform.Czatnia, 26, M3BPlay.C.Balance.Policy!));
        Assert.Equal(0, PolicyRules.ActiveStrikes(s, Platform.Czatnia, 27, M3BPlay.C.Balance.Policy!));
    }

    [Fact]
    public void Expired_strikes_are_dropped_at_settlement()
    {
        var s = M3BPlay.Fresh() with { Week = new CareerWeek(30), LastSettledWeek = 29, Strikes = [new PolicyStrike(Platform.Czatnia, 1)] };
        var (after, _) = M3BPlay.Week(s, M3BPlay.Plan(MainAction.NoAction));
        Assert.True(after.Strikes is not { Length: > 0 });
    }

    [Fact]
    public void A_strike_roll_records_exposure_threshold_and_roll()
    {
        var (_, report) = UntilStrike(M3BPlay.Fresh());
        Assert.Equal((Platform.Czatnia, 4, 6, true, false), (report.Strike!.Platform, report.Strike.Exposure, report.Strike.Threshold, report.Strike.Strike, report.Strike.Banned));
        Assert.InRange(report.Strike.Roll!.Value, 1, 6);
    }

    [Fact]
    public void Third_active_strike_bans_the_platform_and_queues_the_ban_decision()
    {
        var (after, report) = UntilStrike(M3BPlay.Fresh() with { Strikes = [new(Platform.Czatnia, 1), new(Platform.Czatnia, 1)] });
        Assert.True(report.Strike!.Banned);
        Assert.Equal([Platform.Czatnia], after.BannedPlatforms!.Value);
        Assert.Equal("platform-ban", after.PendingDecisions!.Value[0]);
        Assert.Empty(CareerInvariants.Validate(after));
    }

    [Fact]
    public void Ban_without_an_accessible_platform_ends_the_career()
    {
        var s = M3BPlay.Fresh() with { BannedPlatforms = [Platform.PulsLive, Platform.Nurt], Strikes = [new(Platform.Czatnia, 1), new(Platform.Czatnia, 1)] };
        var (after, report) = UntilStrike(s);
        Assert.Equal((EndingReason.AllPlatformsBanned, CareerStatus.Ended), (report.Ending, after.Status));
        Assert.True(after.FinalScore!.WealthPoints > 0);
    }

    [Fact]
    public void Accessible_platforms_exclude_the_current_and_the_banned_ones()
    {
        var s = M3BPlay.Fresh() with { Platform = Platform.Nurt, BannedPlatforms = [Platform.Czatnia] };
        Assert.Equal([Platform.PulsLive], PlatformRules.Accessible(s));
    }

    [Fact]
    public void Policy_strike_effect_adds_strikes_on_the_current_platform()
    {
        var s = EffectApplier.ToCarry(M3BPlay.Fresh() with { Platform = Platform.Nurt, Week = new CareerWeek(40) }, M3BPlay.C, "test", [new Effect(EffectKind.PolicyStrike, null, Fixed.FromInt(2))]);
        Assert.Equal([new PolicyStrike(Platform.Nurt, 40), new PolicyStrike(Platform.Nurt, 40)], s.Strikes!.Value);
    }

    [Fact]
    public void Global_ban_ends_the_career_at_the_next_settlement()
    {
        var s = EffectApplier.ToCarry(M3BPlay.Fresh(), M3BPlay.C, "test", [new Effect(EffectKind.GlobalBan, null, Fixed.Zero)]);
        Assert.Equal(CareerStatus.Active, s.Status);
        var (after, report) = M3BPlay.Week(s, M3BPlay.Plan(MainAction.NoAction));
        Assert.Equal((EndingReason.GlobalBan, CareerStatus.Ended), (report.Ending, after.Status));
    }

    [Fact]
    public void Insolvency_wins_over_a_global_ban()
    {
        var s = EffectApplier.ToCarry(M3BPlay.Fresh(), M3BPlay.C, "test", [new Effect(EffectKind.GlobalBan, null, Fixed.Zero)]) with { CashPln = -2_000 };
        var (after, report) = M3BPlay.Week(s, M3BPlay.Plan(MainAction.NoAction));
        Assert.Equal((EndingReason.Insolvency, EndingReason.Insolvency), (report.Ending, after.Ending));
    }

    [Fact]
    public void Min_strikes_eligibility_reads_the_policy_strike_weeks()
    {
        var c = M3BPlay.C;
        var e = c.Events[0] with { Eligibility = c.Events[0].Eligibility with { MinStrikes = 1 } };
        var s = M3BPlay.Fresh() with { Strikes = [new PolicyStrike(Platform.Czatnia, 1)] };
        Assert.True(EventSelector.IsEligible(s with { LastSettledWeek = 26 }, c.Balance, e, category: null));
        Assert.False(EventSelector.IsEligible(s with { LastSettledWeek = 27 }, c.Balance, e, category: null));
    }

    /// <summary>
    /// With 9,000 PLN (so the <c>High</c> stake is legal), the first seed in 1–400 whose Gambling <c>High</c> no-action
    /// week (exposure 4, threshold 6 on Czatnia) gives a strike.
    /// </summary>
    private static (CareerState State, WeekReport Report) UntilStrike(CareerState s)
    {
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var seeded = s with { CashPln = 9_000, Rng = M3BPlay.Fresh(seed).Rng };
            var (after, report) = M3BPlay.Week(seeded, M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.High));
            if (report.Strike is { Strike: true })
            {
                return (after, report);
            }
        }

        throw new InvalidOperationException("No strike in 400 seeds.");
    }
}
