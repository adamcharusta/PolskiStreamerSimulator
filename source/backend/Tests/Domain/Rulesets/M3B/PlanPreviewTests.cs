using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets.M3B;

public class PlanPreviewTests
{
    [Fact]
    public void Preview_lists_the_wager_terms_and_the_sponsor_breach()
    {
        var s = M3BPlay.Fresh() with { CashPln = 4_000, Contracts = [new SponsorContract("trusted-tea:1", "trusted-tea", SponsorKind.Contract, 1, 4, 12, 300, 0)] };
        var preview = M3BPlay.Engine.PresentPlan(s, M3BPlay.C, M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.Medium)).Value;
        Assert.Equal(500, preview.Wager!.StakePln);
        Assert.Equal(SponsorConsequenceKind.Breach, Assert.Single(preview.Sponsors).Kind);
    }

    [Fact]
    public void Preview_without_risks_is_empty()
    {
        var preview = M3BPlay.Engine.PresentPlan(M3BPlay.Fresh(), M3BPlay.C, M3BPlay.Plan(MainAction.NoAction)).Value;
        Assert.Null(preview.Wager);
        Assert.Empty(preview.Sponsors);
    }

    [Fact]
    public void M3a_preview_is_empty()
    {
        var preview = M3Play.Engine.PresentPlan(M3Play.Fresh(), M3Play.C, new WeekPlan(MainAction.NoAction, Category.Fps)).Value;
        Assert.Null(preview.Wager);
        Assert.True(preview.Sponsors.IsDefaultOrEmpty);
    }

    [Fact]
    public void M1_preview_is_none()
        => Assert.Same(PlanPreview.None, TestWorld.Engine.PresentPlan(TestCareers.Fresh(), TestWorld.Content, new WeekPlan(MainAction.NoAction, Category.Fps)).Value);
}
