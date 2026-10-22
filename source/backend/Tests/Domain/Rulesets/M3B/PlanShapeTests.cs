using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets.M3B;

public class PlanShapeTests
{
    private static readonly ContentSnapshot C = M3BPlay.C;

    [Fact]
    public void M3a_content_offers_none_of_the_new_plans()
    {
        var plans = M3Play.Engine.LegalPlans(M3Play.Fresh(), M3Play.C).Value;
        Assert.DoesNotContain(plans, p => p.Plan.Action is MainAction.PitchFulfillSponsor or MainAction.SideBusinessWork or MainAction.PlanPlatformMigration);
        Assert.DoesNotContain(plans, p => p.Plan.Wager is not null);
    }

    [Fact]
    public void Gambling_plans_have_three_wager_variants_and_others_none()
    {
        var plans = M3BPlay.Engine.LegalPlans(M3BPlay.Fresh(), C).Value;
        Assert.Equal(3, plans.Count(p => p.Plan is { Action: MainAction.NoAction, Category: Category.Gambling, Wager: not null }));
        Assert.DoesNotContain(plans, p => p.Plan.Category != Category.Gambling && p.Plan.Wager is not null);
    }

    [Fact]
    public void Migration_targets_exclude_the_current_platform()
    {
        var s = M3BPlay.Fresh() with { Platform = Platform.Nurt };
        var targets = M3BPlay.Engine.LegalPlans(s, C).Value
            .Where(p => p.Plan is { Action: MainAction.PlanPlatformMigration, Category: Category.Fps })
            .Select(p => p.Plan.TargetPlatform);
        Assert.Equal([Platform.Czatnia, Platform.PulsLive], targets);
    }

    [Theory]
    [InlineData(26, "too-soon")]
    [InlineData(27, null)]
    public void Migration_needs_26_weeks_on_the_platform(int week, string? reason)
        => Assert.Equal(reason, Reason(M3BPlay.Fresh() with { Week = new CareerWeek(week), Energy = Fixed.FromInt(50) }, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt)));

    [Fact]
    public void Migration_to_a_banned_platform_is_disabled()
        => Assert.Equal("platform-banned", Reason(M3BPlay.Fresh() with { Week = new CareerWeek(40), BannedPlatforms = [Platform.Nurt] }, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt)));

    [Theory]
    [InlineData(51, 5_000, "too-early")]
    [InlineData(52, 1_999, "needs-followers")]
    [InlineData(52, 2_000, null)]
    public void Side_business_opens_from_week_52_with_2000_followers(int week, long followers, string? reason)
        => Assert.Equal(reason, Reason(M3BPlay.Fresh() with { Week = new CareerWeek(week), Followers = followers, Energy = Fixed.FromInt(50) }, M3BPlay.Plan(MainAction.SideBusinessWork)));

    [Fact]
    public void Side_business_needs_12_energy()
        => Assert.Equal("not-enough-energy", Reason(M3BPlay.Fresh() with { Week = new CareerWeek(60), Followers = 3_000, Energy = Fixed.FromInt(11) }, M3BPlay.Plan(MainAction.SideBusinessWork)));

    [Fact]
    public void Wager_on_a_non_gambling_plan_is_refused()
    {
        var result = M3BPlay.Engine.StartWeek(M3BPlay.Fresh(), C, M3BPlay.Plan(MainAction.NoAction, Category.Fps, WagerTier.Low), 0);
        Assert.Equal((DomainErrorCode.IllegalAction, "wager-category"), (result.Error.Code, result.Error.Detail));
    }

    [Fact]
    public void Side_business_work_costs_12_energy_and_adds_4_burnout()
    {
        var s = M3BPlay.Fresh() with { Week = new CareerWeek(60), Followers = 3_000, Energy = Fixed.FromInt(50) };
        var started = M3BPlay.Start(s, M3BPlay.Plan(MainAction.SideBusinessWork));
        Assert.Equal(Fixed.FromInt(38), started.Energy);
        Assert.Equal(s.Burnout + Fixed.FromInt(4), started.Burnout);
    }

    [Theory]
    [InlineData(MainAction.PitchFulfillSponsor, 42)]
    [InlineData(MainAction.PlanPlatformMigration, 44)]
    public void Pitch_and_migration_cost_their_energy(MainAction action, int energyAfter)
    {
        var s = M3BPlay.Fresh() with { Week = new CareerWeek(40), Energy = Fixed.FromInt(50) };
        var started = M3BPlay.Start(s, M3BPlay.Plan(action, target: action == MainAction.PlanPlatformMigration ? Platform.Nurt : null));
        Assert.Equal(Fixed.FromInt(energyAfter), started.Energy);
    }

    [Fact]
    public void Wager_on_m3a_content_is_unsupported()
    {
        var result = M3Play.Engine.StartWeek(M3Play.Fresh(), M3Play.C, M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.Low), 0);
        Assert.Equal((DomainErrorCode.IllegalAction, "unsupported-action"), (result.Error.Code, result.Error.Detail));
    }

    [Theory]
    [InlineData(null, "missing-target")]
    [InlineData(Platform.Czatnia, "same-platform")]
    public void Migration_needs_another_platform_as_its_target(Platform? target, string reason)
    {
        var result = M3BPlay.Engine.StartWeek(M3BPlay.Fresh() with { Week = new CareerWeek(40) }, C, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: target), 0);
        Assert.Equal((DomainErrorCode.IllegalAction, reason), (result.Error.Code, result.Error.Detail));
    }

    [Fact]
    public void Stake_must_fit_a_quarter_of_spendable_cash()
    {
        var plan = M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.Low);
        Assert.Null(Reason(M3BPlay.Fresh() with { CashPln = 400 }, plan));
        Assert.Equal("needs-cash", Reason(M3BPlay.Fresh() with { CashPln = 399 }, plan));
    }

    private static string? Reason(CareerState s, WeekPlan plan)
        => M3BPlay.Engine.LegalPlans(s, C).Value.Single(p => p.Plan == plan).DisabledReason;
}
