using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Application.Leaderboard;
using PolskiStreamerSimulator.Tests.Application.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EfLeaderboardStoreTests(SqlServerFixture sql) : LeaderboardStoreContract
{
    protected override async Task<(ISeasonStore Seasons, ILeaderboardStore Entries)> CreateStoresAsync()
    {
        var factory = await ContentReadyAsync();
        return (new EfSeasonStore(factory), new EfLeaderboardStore(factory));
    }

    [Fact]
    public async Task An_entry_outlives_the_deletion_of_its_career()
    {
        var factory = await ContentReadyAsync();
        var kit = AppTestKit.On(new EfCareerStore(factory), new EfContentStore(factory, TimeProvider.System), new EfSeasonStore(factory), new EfLeaderboardStore(factory), TestPaths.ContentM3);
        var created = await kit.NewCareerAsync(ranked: true);
        await kit.RetireAsync(created.Career);
        Assert.True((await kit.PublishAsync(created.CareerId, "KEEP")).IsSuccess);

        kit.Time.Now = AppTestKit.Start.AddMonths(13);
        Assert.Equal(1, await kit.Sweep().RunAsync(Ct));

        Assert.Null(await kit.Careers.FindAsync(created.CareerId, Ct));
        var entry = Assert.Single(await kit.Entries.TopAsync(1, 100, Ct));
        Assert.Equal((created.CareerId, "KEEP"), (entry.CareerId, entry.Nickname));
    }

    [Fact]
    public async Task A_concurrent_duplicate_publication_stores_one_entry()
    {
        var factory = await ContentReadyAsync();
        var seasons = new EfSeasonStore(factory);
        var entries = new EfLeaderboardStore(factory);
        await seasons.EnsureCurrentAsync("m1", M1, M1, At, Ct);
        var careerId = Guid.NewGuid();

        var added = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => entries.AddAsync(LeaderboardRulesTests.Entry(careerId, 100 + i, 1, At), Ct)));

        Assert.Single(added, a => a);
        Assert.Single(await entries.TopAsync(1, 100, Ct));
    }

    private async Task<TestDbContextFactory> ContentReadyAsync()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);
        var contents = new EfContentStore(factory, TimeProvider.System);
        await contents.EnsureStoredAsync(new TestCurrentContent(TestPaths.ContentM1).Files, Ct);
        await contents.EnsureStoredAsync(new TestCurrentContent(TestPaths.ContentM3).Files, Ct);
        return factory;
    }
}
