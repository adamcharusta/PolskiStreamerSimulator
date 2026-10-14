using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public sealed class PlayerDecisionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(1040)]
    public void M1_never_requires_a_player_decision(int week)
    {
        var s = TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, 3) with { Week = new CareerWeek(week) };

        Assert.False(new M1Ruleset().RequiresPlayerDecision(s, TestWorld.Content));
        Assert.False(TestWorld.Engine.RequiresPlayerDecision(s, TestWorld.Content).Value);
    }

    [Fact]
    public void The_engine_asks_the_careers_own_ruleset()
    {
        var s = TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, 3) with { Week = new CareerWeek(5) };

        Assert.True(PausingRuleset.Engine(5).RequiresPlayerDecision(s, TestWorld.Content).Value);
        Assert.False(PausingRuleset.Engine(6).RequiresPlayerDecision(s, TestWorld.Content).Value);
    }

    [Fact]
    public void Other_content_is_rejected_before_the_ruleset_is_asked()
    {
        var s = TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, 3) with { Week = new CareerWeek(5) };

        var r = PausingRuleset.Engine(5).RequiresPlayerDecision(s, TestWorld.Content with { Digest = "other" });

        Assert.Equal(DomainErrorCode.ContentMismatch, r.Error.Code);
    }
}
