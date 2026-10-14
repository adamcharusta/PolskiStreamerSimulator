using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Persistence;

/// <summary>Behaviour every <see cref="ICareerStore"/> shows; run against the in-memory and the SQL Server store.</summary>
public abstract class CareerStoreContract
{
    private static readonly DateTimeOffset Created = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddMinutes(5);

    protected abstract Task<ICareerStore> CreateStoreAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected static NewCareer SampleCareer(ulong seed = 1)
        => new(Guid.CreateVersion7(Created), "Streamer", TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, seed), [1, 2, 3], Created);

    private static CareerState Advanced(CareerState s) => s with { StateVersion = s.StateVersion + 1 };

    private static NewAction Entry(Guid commandId, int step = 0, string? report = null)
        => new(commandId, step, step == 0 ? ActionKind.SettleWeek : ActionKind.AutoWeek, """{"request":1}""", """{"response":1}""", report);

    private async Task<(ICareerStore Store, NewCareer Career)> StoreWithCareerAsync()
    {
        var store = await CreateStoreAsync();
        var career = SampleCareer();
        await store.CreateAsync(career, Ct);
        return (store, career);
    }

    [Fact]
    public async Task A_created_career_is_found_with_its_state_and_secret_hash()
    {
        var (store, career) = await StoreWithCareerAsync();

        var found = await store.FindAsync(career.Id, Ct);

        Assert.NotNull(found);
        Assert.Equal("Streamer", found.Nickname);
        Assert.Equal(StateHash.Of(career.State), StateHash.Of(found.State));
        Assert.Equal(Created, found.CreatedAt);
        Assert.Equal(Created, found.LastActionAt);
        Assert.Equal(career.SecretHash, await store.FindSecretHashAsync(career.Id, Ct));
    }

    [Fact]
    public async Task An_unknown_career_is_not_found()
    {
        var store = await CreateStoreAsync();

        Assert.Null(await store.FindAsync(Guid.NewGuid(), Ct));
        Assert.Null(await store.FindSecretHashAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Append_replaces_the_state_and_logs_the_command()
    {
        var (store, career) = await StoreWithCareerAsync();
        var next = Advanced(career.State);
        var command = Guid.NewGuid();

        Assert.Equal(AppendOutcome.Appended, await store.AppendAsync(career.Id, career.State.StateVersion, next, Entry(command), Later, Ct));

        var found = await store.FindAsync(career.Id, Ct);
        Assert.Equal(next.StateVersion, found!.State.StateVersion);
        Assert.Equal(Later, found.LastActionAt);
        var logged = Assert.Single(await store.FindCommandAsync(career.Id, command, Ct));
        Assert.Equal(1, logged.Sequence);
        Assert.Equal(ActionKind.SettleWeek, logged.Kind);
        Assert.Equal(next.StateVersion, logged.StateVersionAfter);
        Assert.Equal(StateHash.Of(next), logged.StateHashAfter);
        Assert.Equal("""{"request":1}""", logged.RequestJson);
        Assert.Equal("""{"response":1}""", logged.ResponseJson);
        Assert.Equal(Later, logged.CreatedAt);
    }

    [Fact]
    public async Task A_stale_version_changes_nothing()
    {
        var (store, career) = await StoreWithCareerAsync();
        var command = Guid.NewGuid();

        var outcome = await store.AppendAsync(career.Id, career.State.StateVersion + 1, Advanced(Advanced(career.State)), Entry(command), Later, Ct);

        Assert.Equal(AppendOutcome.VersionConflict, outcome);
        Assert.Equal(career.State.StateVersion, (await store.FindAsync(career.Id, Ct))!.State.StateVersion);
        Assert.Empty(await store.FindCommandAsync(career.Id, command, Ct));
    }

    [Fact]
    public async Task A_repeated_command_step_is_rejected_and_changes_nothing()
    {
        var (store, career) = await StoreWithCareerAsync();
        var command = Guid.NewGuid();
        var v1 = Advanced(career.State);
        Assert.Equal(AppendOutcome.Appended, await store.AppendAsync(career.Id, career.State.StateVersion, v1, Entry(command), Later, Ct));

        var outcome = await store.AppendAsync(career.Id, v1.StateVersion, Advanced(v1), Entry(command), Later, Ct);

        Assert.Equal(AppendOutcome.DuplicateCommand, outcome);
        Assert.Single(await store.FindCommandAsync(career.Id, command, Ct));
        Assert.Equal(v1.StateVersion, (await store.FindAsync(career.Id, Ct))!.State.StateVersion);
    }

    [Fact]
    public async Task Command_steps_come_back_in_order_with_consecutive_sequences()
    {
        var (store, career) = await StoreWithCareerAsync();
        var batch = Guid.NewGuid();
        var v1 = Advanced(career.State);
        var v2 = Advanced(v1);
        await store.AppendAsync(career.Id, career.State.StateVersion, v1, Entry(batch, 1), Later, Ct);
        await store.AppendAsync(career.Id, v1.StateVersion, v2, Entry(batch, 2), Later, Ct);

        var steps = await store.FindCommandAsync(career.Id, batch, Ct);

        Assert.Equal([1, 2], steps.Select(s => s.Step));
        Assert.Equal([1, 2], steps.Select(s => s.Sequence));
        Assert.All(steps, s => Assert.Equal(ActionKind.AutoWeek, s.Kind));
    }

    [Fact]
    public async Task The_latest_report_is_the_newest_entry_that_has_one()
    {
        var (store, career) = await StoreWithCareerAsync();
        Assert.Null(await store.FindLatestReportAsync(career.Id, Ct));
        var v1 = Advanced(career.State);
        var v2 = Advanced(v1);
        await store.AppendAsync(career.Id, career.State.StateVersion, v1, Entry(Guid.NewGuid(), report: """{"week":1}"""), Later, Ct);
        await store.AppendAsync(career.Id, v1.StateVersion, v2, Entry(Guid.NewGuid()), Later, Ct);

        var latest = await store.FindLatestReportAsync(career.Id, Ct);

        Assert.Equal("""{"week":1}""", latest!.ReportJson);
        Assert.Equal(1, latest.Sequence);
    }
}
