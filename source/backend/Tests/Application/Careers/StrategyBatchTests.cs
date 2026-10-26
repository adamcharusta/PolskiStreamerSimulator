using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

/// <summary>M3-C batches: 4, 13 or 52 weeks, played by the player's strategy (#22).</summary>
public sealed class StrategyBatchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static StrategyInput Input(Strategy s) => StrategyInput.From(s);

    private static Task<AppResult<BatchOutcome>> RunAsync(AppTestKit kit, CreatedCareer career, long version, int weeks, StrategyInput? strategy, Guid? commandId = null)
        => kit.BatchHandler().Handle(new RunBatch(career.CareerId, career.Secret, commandId ?? Guid.NewGuid(), new BatchInput(version, weeks, strategy)), Ct);

    [Theory]
    [InlineData(4)]
    [InlineData(52)]
    public async Task A_batch_plays_four_or_fifty_two_weeks(int weeks)
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var outcome = (await RunAsync(kit, career, career.Career.Version, weeks, null)).Value;
        Assert.Equal((weeks, BatchStopReason.Completed), (outcome.WeeksCommitted, outcome.StopReason));
        Assert.Equal(weeks, kit.Store.LogCount(career.CareerId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(53)]
    public async Task Other_lengths_are_rejected(int weeks)
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        Assert.Equal(ErrorCodes.ValidationFailed, (await RunAsync(kit, career, career.Career.Version, weeks, null)).Error.Code);
    }

    [Fact]
    public async Task A_strategy_chooses_the_weeks_with_its_reasons()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.NewCareerAsync();
        var strategy = Strategy.Default with { Upgrades = false, Routine = [MainAction.ManageCommunity], RestBelowEnergy = 0 };
        var outcome = (await RunAsync(kit, career, career.Career.Version, 4, Input(strategy))).Value;
        Assert.Contains(outcome.Weeks, w => w.PlanReason.Code == "routine-step" && w.Plan.MainAction == MainAction.ManageCommunity);
    }

    [Fact]
    public async Task An_invalid_strategy_is_rejected_with_its_codes()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.NewCareerAsync();
        var refused = await RunAsync(kit, career, career.Career.Version, 4, Input(Strategy.Default with { RestBelowEnergy = 99 }));
        Assert.Equal((ErrorCodes.ValidationFailed, "strategy:energy-range"), (refused.Error.Code, refused.Error.Detail));
    }

    [Fact]
    public async Task A_repeat_with_the_same_strategy_replays_and_another_strategy_is_refused()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        var first = (await RunAsync(kit, career, career.Career.Version, 4, Input(Strategy.Default), command)).Value;
        var again = (await RunAsync(kit, career, career.Career.Version, 4, Input(Strategy.Default), command)).Value;
        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(again));
        Assert.Equal(first.WeeksCommitted, kit.Store.LogCount(career.CareerId));
        var other = await RunAsync(kit, career, career.Career.Version, 4, Input(Strategy.Default with { Risk = RiskAppetite.Bold }), command);
        Assert.Equal(ErrorCodes.CommandIdReused, other.Error.Code);
    }

    [Fact]
    public void A_request_without_a_strategy_serializes_as_before()
        => Assert.Equal("""{"expectedVersion":3,"weeks":13}""", ContractJson.Serialize(new BatchInput(3, 13)));

    [Fact]
    public void Strategy_input_round_trips_to_the_strategy()
    {
        var bold = Strategy.Default with { CategoryPlan = CategoryPlan.Focus, Focus = Category.Gambling, Wager = WagerTier.Low, Risk = RiskAppetite.Bold };
        Assert.Equal(bold, Input(bold).ToStrategy());
    }
}
