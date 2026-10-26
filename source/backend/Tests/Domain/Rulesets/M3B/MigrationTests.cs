using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets.M3B;

public class MigrationTests
{
    private static readonly MigrationBalance M = M3BPlay.C.Balance.Migration!;

    private static CareerState Ready(long followers = 10_000)
        => M3BPlay.Fresh() with { Week = new CareerWeek(30), LastSettledWeek = 29, Followers = followers, Credibility = Fixed.FromInt(50), Drama = Fixed.Zero, Energy = Fixed.FromInt(60) };

    [Theory]
    [InlineData(50, 0, false, false, 60)]
    [InlineData(100, 0, true, false, 80)]
    [InlineData(0, 100, false, true, 20)]
    [InlineData(50, 0, false, true, 50)]
    public void Retention_follows_the_clamped_formula(int credibility, int drama, bool announced, bool forced, int percent)
    {
        var s = Ready() with { Credibility = Fixed.FromInt(credibility), Drama = Fixed.FromInt(drama) };
        s = announced ? CareerFlags.Set(s, PlatformRules.AnnouncedFlag(Platform.Nurt), 4) : s;
        Assert.Equal(Fixed.FromRatio(percent, 100), PlatformRules.Retention(s, Platform.Nurt, forced, M));
    }

    [Fact]
    public void First_use_announces_without_moving()
    {
        var (after, report) = M3BPlay.Week(Ready(), M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt));
        Assert.True(CareerFlags.IsActive(after, "migration-announced:Nurt"));
        Assert.Equal(Platform.Czatnia, Economy.Current(after));
        Assert.Contains(report.StatChanges, l => l is { Code: "stat:Credibility", Detail: "migration" } && l.Delta == Fixed.FromInt(-1));
        Assert.Null(report.Migration);
    }

    [Fact]
    public void Unannounced_preview_does_not_execute()
    {
        var estimate = M3BPlay.Engine.PresentPlan(Ready(), M3BPlay.C, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt)).Value.Migration!;
        Assert.False(estimate.Executes);
        Assert.Equal((Platform.Czatnia, Platform.Nurt, M3BPlay.C.Balance.Platforms![Platform.Nurt]), (estimate.From, estimate.To, estimate.Target));
    }

    [Fact]
    public void Second_use_moves_with_the_shown_range_and_reports_the_exact_count()
    {
        var s = CareerFlags.Set(Ready(), PlatformRules.AnnouncedFlag(Platform.Nurt), 4);
        var estimate = M3BPlay.Engine.PresentPlan(s, M3BPlay.C, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt)).Value.Migration!;
        Assert.Equal((true, Fixed.FromRatio(65, 100), Fixed.FromRatio(75, 100)), (estimate.Executes, estimate.RetentionMin, estimate.RetentionMax));
        Assert.Equal((6_500L, 7_500L), (estimate.FollowersMin, estimate.FollowersMax));

        var started = M3BPlay.Start(s, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt));
        Assert.False(CareerFlags.IsActive(started, PlatformRules.AnnouncedFlag(Platform.Nurt)));
        var (after, report) = M3BPlay.SettleAll(started);
        var moved = report.Migration!;
        Assert.True(moved.Retention >= estimate.RetentionMin && moved.Retention <= estimate.RetentionMax);
        Assert.Equal((Platform.Czatnia, Platform.Nurt, 31), (moved.From, moved.To, after.PlatformSinceWeek));
        Assert.Equal(moved.FollowersAfter, after.Followers);
        Assert.Equal(moved.FollowersBefore - moved.FollowersAfter, after.FormerFollowers);
        Assert.Contains(report.StatChanges, l => l is { Code: "followers:migration", Detail: "Nurt" });
        Assert.Equal(report.FollowersGained - report.FollowersLost, report.FollowersNet);
        Assert.Empty(CareerInvariants.Validate(after));
    }

    [Fact]
    public void Migration_with_zero_followers_moves_and_reports_zero()
    {
        var (after, report) = M3BPlay.Week(CareerFlags.Set(Ready(followers: 0), PlatformRules.AnnouncedFlag(Platform.PulsLive), 4), M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.PulsLive));

        // The week's audience step runs before the move, so a career that starts with 0 followers can gain some that week;
        // the retention applies to exactly that gained count.
        var moved = report.Migration!;
        Assert.Equal(report.FollowersGained, moved.FollowersBefore);
        Assert.Equal(moved.FollowersAfter, after.Followers);
        Assert.Equal(moved.FollowersBefore - moved.FollowersAfter, after.FormerFollowers ?? 0);
        Assert.Equal(Platform.PulsLive, Economy.Current(after));
    }

    [Fact]
    public void Move_ends_contracts_tied_to_the_old_platform_without_penalty()
    {
        var s = CareerFlags.Set(Ready() with { Platform = Platform.PulsLive, Contracts = [new SponsorContract("puls-gear:28", "puls-gear", SponsorKind.Contract, 28, 31, 35, 500, 0)] }, PlatformRules.AnnouncedFlag(Platform.Nurt), 4);
        var (after, report) = M3BPlay.Week(s, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt));
        Assert.Empty(after.Contracts!.Value);
        Assert.DoesNotContain(report.Costs, l => l.Code == "cost:sponsor-penalty");
    }

    [Fact]
    public void Strikes_on_the_old_platform_stay()
    {
        var s = CareerFlags.Set(Ready() with { Strikes = [new PolicyStrike(Platform.Czatnia, 20)] }, PlatformRules.AnnouncedFlag(Platform.Nurt), 4);
        var (after, _) = M3BPlay.Week(s, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt));
        Assert.Equal(Platform.Nurt, Economy.Current(after));
        Assert.Equal([new PolicyStrike(Platform.Czatnia, 20)], after.Strikes!.Value);
    }

    [Fact]
    public void Announcement_expires_after_four_weeks()
    {
        var announced = CareerFlags.Set(Ready(), PlatformRules.AnnouncedFlag(Platform.Nurt), 4);
        Assert.True(CareerFlags.IsActive(announced with { Week = new CareerWeek(33) }, PlatformRules.AnnouncedFlag(Platform.Nurt)));
        var late = announced with { Week = new CareerWeek(34), LastSettledWeek = 33 };
        Assert.False(CareerFlags.IsActive(late, PlatformRules.AnnouncedFlag(Platform.Nurt)));
        var (after, report) = M3BPlay.Week(late, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt));
        Assert.Null(report.Migration);
        Assert.Equal(Platform.Czatnia, Economy.Current(after));
        Assert.True(CareerFlags.IsActive(after, PlatformRules.AnnouncedFlag(Platform.Nurt)));
    }
}
