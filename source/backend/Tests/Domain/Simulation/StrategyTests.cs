using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class StrategyTests
{
    [Fact]
    public void Default_strategy_is_valid()
    {
        Assert.Empty(Strategy.Default.Errors());
        Assert.Equal([MainAction.NoAction, MainAction.NoAction, MainAction.NoAction, MainAction.TrainCategory], Strategy.Default.Routine);
        Assert.Equal((30, 70, 0L, RiskAppetite.Balanced), (Strategy.Default.RestBelowEnergy, Strategy.Default.RestAboveBurnout, Strategy.Default.ReservePln, Strategy.Default.Risk));
    }

    [Fact]
    public void Focus_must_match_the_category_plan()
    {
        Assert.Equal(["focus-missing"], (Strategy.Default with { CategoryPlan = CategoryPlan.Focus }).Errors());
        Assert.Equal(["focus-unexpected"], (Strategy.Default with { Focus = Category.Chess }).Errors());
        Assert.Empty((Strategy.Default with { CategoryPlan = CategoryPlan.Focus, Focus = Category.Chess }).Errors());
    }

    [Theory]
    [InlineData(0, "routine-length")]
    [InlineData(9, "routine-length")]
    public void Routine_has_one_to_eight_steps(int length, string error)
        => Assert.Equal([error], (Strategy.Default with { Routine = [.. Enumerable.Repeat(MainAction.NoAction, length)] }).Errors());

    [Theory]
    [InlineData(MainAction.Rest)]
    [InlineData(MainAction.ImproveProduction)]
    [InlineData(MainAction.SideBusinessWork)]
    [InlineData(MainAction.PlanPlatformMigration)]
    public void Routine_takes_only_free_week_actions(MainAction action)
        => Assert.Equal(["routine-action"], (Strategy.Default with { Routine = [MainAction.NoAction, action] }).Errors());

    [Fact]
    public void Thresholds_and_reserve_stay_in_range()
    {
        Assert.Equal(["energy-range"], (Strategy.Default with { RestBelowEnergy = 81 }).Errors());
        Assert.Equal(["burnout-range"], (Strategy.Default with { RestAboveBurnout = 49 }).Errors());
        Assert.Equal(["reserve-range"], (Strategy.Default with { ReservePln = -1 }).Errors());
        Assert.Equal(["reserve-range"], (Strategy.Default with { ReservePln = 1_000_001 }).Errors());
    }

    [Fact]
    public void Equality_compares_the_routine_by_steps()
        => Assert.Equal(Strategy.Default, Strategy.Default with { Routine = [MainAction.NoAction, MainAction.NoAction, MainAction.NoAction, MainAction.TrainCategory] });
}
