using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets.M3B;

public class PlatformFactorTests
{
    [Fact]
    public void Platform_factors_replace_the_scalar_ones()
    {
        var b = PlatformRules.Effective(M3BPlay.C.Balance, M3BPlay.Fresh() with { Platform = Platform.PulsLive }, Category.Fps);
        Assert.Equal((Fixed.FromRatio(132, 100), Fixed.FromRatio(85, 100), Fixed.FromRatio(110, 100)), (b.PlatformReach, b.PlatformSubYield, b.PlatformAdYield));
        var nurtChess = PlatformRules.Effective(M3BPlay.C.Balance, M3BPlay.Fresh() with { Platform = Platform.Nurt }, Category.Chess);
        Assert.Equal(Fixed.FromRatio(102, 100), nurtChess.PlatformReach);   // 0.85 × 1.20
    }

    [Fact]
    public void Balance_without_platforms_is_returned_unchanged()
    {
        var balance = M3Play.C.Balance;
        Assert.Same(balance, PlatformRules.Effective(balance, M3Play.Fresh(), Category.Fps));
    }

    [Fact]
    public void Puls_live_draws_more_viewers_than_czatnia_from_the_same_state()
    {
        var czatnia = M3BPlay.Fresh(9) with { Followers = 5_000 };
        var (_, onCzatnia) = M3BPlay.Week(czatnia, M3BPlay.Plan(MainAction.NoAction));
        var (_, onPuls) = M3BPlay.Week(czatnia with { Platform = Platform.PulsLive }, M3BPlay.Plan(MainAction.NoAction));
        Assert.True(onPuls.LiveViewers > onCzatnia.LiveViewers);
    }

    [Fact]
    public void A_week_without_new_drama_decays_drama_by_one()
    {
        var decayed = 0;
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var before = M3BPlay.Fresh(seed) with { Drama = Fixed.FromInt(30) };
            var (_, report) = M3BPlay.Week(before, M3BPlay.Plan(MainAction.NoAction, Category.JustChatting));
            var decay = report.StatChanges.Where(l => l is { Code: "stat:Drama", Detail: "decay" }).ToList();
            var added = PolicyRules.AddedDrama(report.StatChanges.Where(l => l.Detail != "decay"));
            Assert.Equal(!added, decay.Count == 1);
            if (decay.Count == 1)
            {
                Assert.Equal(-Fixed.One, decay[0].Delta);
                decayed++;
            }
        }

        Assert.True(decayed > 0);
    }

    [Fact]
    public void Drama_never_decays_below_zero()
    {
        var (after, report) = M3BPlay.Week(M3BPlay.Fresh(4) with { Drama = Fixed.Zero }, M3BPlay.Plan(MainAction.NoAction, Category.JustChatting));
        Assert.True(after.Drama >= Fixed.Zero);
        Assert.DoesNotContain(report.StatChanges, l => l.Detail == "decay");
    }

    [Fact]
    public void M3a_content_never_decays_drama()
    {
        var (_, report) = M3Play.SettleAll(M3Play.Start(M3Play.Fresh() with { Drama = Fixed.FromInt(30) }, MainAction.NoAction, Category.JustChatting));
        Assert.DoesNotContain(report.StatChanges, l => l.Detail == "decay");
    }
}
