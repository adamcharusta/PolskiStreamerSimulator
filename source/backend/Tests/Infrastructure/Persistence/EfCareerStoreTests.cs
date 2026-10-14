using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Application.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EfCareerStoreTests(SqlServerFixture sql) : CareerStoreContract
{
    private static readonly ContentFiles Files = new(TestContentFiles.Balance, TestContentFiles.Events);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected override async Task<ICareerStore> CreateStoreAsync() => new EfCareerStore(await ContentReadyAsync());

    private async Task<TestDbContextFactory> ContentReadyAsync(params FailingCommandInterceptor[] interceptors)
    {
        var factory = await sql.MigratedDatabaseAsync(Ct, interceptors);
        await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(Files, Ct);
        return factory;
    }

    [Fact]
    public async Task A_failed_log_insert_leaves_the_career_unchanged()
    {
        var interceptor = new FailingCommandInterceptor("INSERT INTO [game].[career_actions]");
        var factory = await ContentReadyAsync(interceptor);
        var store = new EfCareerStore(factory);
        var career = SampleCareer();
        await store.CreateAsync(career, Ct);
        var next = career.State with { StateVersion = career.State.StateVersion + 1 };
        var entry = new NewAction(Guid.NewGuid(), 0, ActionKind.SettleWeek, "{}", "{}", null);

        interceptor.Armed = true;
        var error = await Assert.ThrowsAsync<DbUpdateException>(
            () => store.AppendAsync(career.Id, career.State.StateVersion, next, entry, DateTimeOffset.UnixEpoch, Ct));
        interceptor.Armed = false;

        Assert.IsType<InvalidOperationException>(error.InnerException);

        Assert.Equal(career.State.StateVersion, (await store.FindAsync(career.Id, Ct))!.State.StateVersion);
        await using var db = factory.CreateDbContext();
        Assert.Equal(0, await db.CareerActions.CountAsync(Ct));
    }

    [Fact]
    public async Task A_confirmed_step_survives_a_new_store_on_the_same_database()
    {
        var factory = await ContentReadyAsync();
        var career = SampleCareer();
        var first = new EfCareerStore(factory);
        await first.CreateAsync(career, Ct);
        var settled = career.State with { StateVersion = career.State.StateVersion + 1 };
        var command = Guid.NewGuid();
        await first.AppendAsync(
            career.Id, career.State.StateVersion, settled, new NewAction(command, 0, ActionKind.SettleWeek, "{}", "{}", """{"week":1}"""), DateTimeOffset.UnixEpoch, Ct);

        var reopened = new EfCareerStore(new TestDbContextFactory(factory.Options));

        Assert.Equal(StateHash.Of(settled), StateHash.Of((await reopened.FindAsync(career.Id, Ct))!.State));
        Assert.Equal(settled.StateVersion, Assert.Single(await reopened.FindCommandAsync(career.Id, command, Ct)).StateVersionAfter);
        Assert.Equal("""{"week":1}""", (await reopened.FindLatestReportAsync(career.Id, Ct))!.ReportJson);
    }
}
