using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.Shared;
using PolskiStreamerSimulator.Domain.Scoring;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Scoring;

/// <summary>Every ending through the engine on M3-B content, each with a final score and a summary (#23).</summary>
public class EndingFlowTests
{
    private static CareerState Settle(CareerState s) => M3BPlay.Week(s, M3BPlay.Plan(MainAction.NoAction)).State;

    public static TheoryData<EndingReason> Endings => [.. Enum.GetValues<EndingReason>()];

    private static CareerState EndWith(EndingReason reason)
    {
        var s = M3BPlay.Fresh();
        return reason switch
        {
            EndingReason.Completed => Settle(s with { Week = new CareerWeek(CareerWeek.Last), LastSettledWeek = CareerWeek.Last - 1 }),
            EndingReason.Insolvency => Settle(s with { CashPln = -5_000 }),
            EndingReason.EarlyRetirement => M3BPlay.Engine.Retire(s, M3BPlay.C, s.StateVersion).Value,
            EndingReason.GlobalBan => Settle(EffectApplier.ToCarry(s, M3BPlay.C, "test", [new Effect(EffectKind.GlobalBan, null, Fixed.Zero)])),
            _ => Settle(s with { BannedPlatforms = [Platform.Czatnia, Platform.PulsLive, Platform.Nurt] }),
        };
    }

    [Theory]
    [MemberData(nameof(Endings))]
    public void Every_ending_has_a_score_and_a_summary(EndingReason reason)
    {
        var ended = EndWith(reason);
        Assert.Equal((CareerStatus.Ended, reason), (ended.Status, ended.Ending));
        Assert.NotNull(ended.FinalScore);
        Assert.Empty(CareerInvariants.Validate(ended));
        var summary = EndingSummary.Of(ended, M3BPlay.C)!;
        Assert.Equal(reason, summary.Reason);
        Assert.Equal(ended.LastSettledWeek, summary.WeeksPlayed);
    }

    [Fact]
    public void An_active_career_has_no_summary() => Assert.Null(EndingSummary.Of(M3BPlay.Fresh(), M3BPlay.C));

    [Theory]
    [InlineData(80, 20, Reputation.Trusted)]
    [InlineData(80, 60, Reputation.Controversial)]
    [InlineData(40, 59, Reputation.Steady)]
    public void Reputation_follows_credibility_and_drama(int credibility, int drama, Reputation expected)
    {
        var ended = EndWith(EndingReason.EarlyRetirement) with { Credibility = Fixed.FromInt(credibility), Drama = Fixed.FromInt(drama) };
        Assert.Equal(expected, EndingSummary.Of(ended, M3BPlay.C)!.Reputation);
    }

    [Fact]
    public void Summary_lists_platforms_the_dominant_category_and_earned_achievements_with_points()
    {
        var c = M3BPlay.C with { Balance = M3BPlay.C.Balance with { Achievements = AchievementRulesTests.Defs } };
        var s = M3BPlay.Fresh() with
        {
            Platform = Platform.Nurt,
            PlatformSinceWeek = 30,
            BannedPlatforms = [Platform.Czatnia],
            Followers = 20_000,
            Metrics = M3BPlay.Fresh().Metrics with { WeeksPerCategory = CategoryValues<int>.Uniform(1).With(Category.Chess, 40) },
        };
        var retired = M3BPlay.Engine.Retire(s, c, s.StateVersion).Value;
        var summary = EndingSummary.Of(retired, c)!;
        Assert.Equal((Platform.Nurt, Category.Chess), (summary.Platform, summary.DominantCategory));
        Assert.Equal([Platform.Czatnia], summary.BannedPlatforms);
        Assert.Equal([new EarnedAchievement("fans", 1), new EarnedAchievement("moved", 2)], summary.Achievements);
    }
}
