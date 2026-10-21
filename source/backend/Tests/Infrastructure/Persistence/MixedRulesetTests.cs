using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure.Persistence;

/// <summary>An M1 career created before the switch to the M3 content and an M3 career share one database.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MixedRulesetTests(SqlServerFixture sql)
{
    [Fact]
    public async Task M1_and_m3_careers_each_play_a_week_on_their_own_snapshot()
    {
        var factory = await sql.MigratedDatabaseAsync(TestContext.Current.CancellationToken);
        var kit = AppTestKit.On(new EfCareerStore(factory), new EfContentStore(factory, TimeProvider.System), TestPaths.ContentM1);
        var m1 = await kit.NewCareerAsync();
        kit.SwitchCurrentContent(TestPaths.ContentM3);
        var m3 = await kit.NewCareerAsync();

        var playedM1 = await kit.PlayManualWeekAsync(m1);
        var playedM3 = await kit.PlayManualWeekAsync(m3);

        Assert.Equal(("m1-prototype.1", NextStepKind.PlanWeek, 1), (playedM1.RulesetId, playedM1.Next.Kind, playedM1.LastReport!.Week));
        Assert.Equal(("m3-systems.1", NextStepKind.ResolveDecision, 1), (playedM3.RulesetId, playedM3.Next.Kind, playedM3.LastReport!.Week));
        Assert.Equal(ContractJson.Serialize(playedM3), ContractJson.Serialize(await kit.GetAsync(m3.CareerId)));
    }
}
