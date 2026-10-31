using System.Security.Cryptography;
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

    // A secret hash is always a 32-byte SHA-256; the SQL column is binary(32).
    protected static NewCareer SampleCareer(ulong seed = 1, CreationHashes? creation = null)
        => new(
            Guid.CreateVersion7(Created),
            "Streamer",
            TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, seed),
            SHA256.HashData("secret"u8),
            SHA256.HashData("recovery"u8),
            creation,
            Created);

    private static CreationHashes Creation(string id) => new(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id)), SHA256.HashData("request"u8));

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
        Assert.Equal(Created.AddMonths(6), found.ExpiresAt);
        Assert.Null(found.EndedAt);
        Assert.Equal(career.SecretHash, await store.FindSecretHashAsync(career.Id, Ct));
        var credentials = await store.FindCredentialsAsync(career.Id, Ct);
        Assert.Equal(career.SecretHash, credentials!.SecretHash);
        Assert.Equal(career.RecoveryHash, credentials.RecoveryHash);
        Assert.Null(credentials.CreationRequestHash);
    }

    [Fact]
    public async Task A_creation_hash_finds_its_career_and_a_second_career_under_it_is_refused()
    {
        var store = await CreateStoreAsync();
        var career = SampleCareer(creation: Creation("first"));
        Assert.Equal(CreateOutcome.Created, await store.CreateAsync(career, Ct));

        var duplicate = SampleCareer(2, Creation("first")) with { Id = Guid.CreateVersion7(Later) };

        Assert.Equal(CreateOutcome.DuplicateCreation, await store.CreateAsync(duplicate, Ct));
        Assert.Null(await store.FindAsync(duplicate.Id, Ct));
        Assert.Equal(career.Id, await store.FindByCreationAsync(Creation("first").CreationHash, Ct));
        Assert.Null(await store.FindByCreationAsync(Creation("other").CreationHash, Ct));
        Assert.Equal(SHA256.HashData("request"u8), (await store.FindCredentialsAsync(career.Id, Ct))!.CreationRequestHash);
    }

    [Fact]
    public async Task Careers_without_a_creation_hash_do_not_collide()
    {
        var store = await CreateStoreAsync();

        Assert.Equal(CreateOutcome.Created, await store.CreateAsync(SampleCareer(), Ct));
        Assert.Equal(CreateOutcome.Created, await store.CreateAsync(SampleCareer(2) with { Id = Guid.CreateVersion7(Later) }, Ct));
    }

    [Fact]
    public async Task Replacing_credentials_keeps_the_state_the_last_action_and_the_expiry()
    {
        var (store, career) = await StoreWithCareerAsync();
        var newSecret = SHA256.HashData("new secret"u8);

        Assert.True(await store.ReplaceCredentialsAsync(career.Id, newSecret, null, null, Later, Ct));

        var credentials = await store.FindCredentialsAsync(career.Id, Ct);
        Assert.Equal(newSecret, credentials!.SecretHash);
        Assert.Equal(career.RecoveryHash, credentials.RecoveryHash);
        var found = await store.FindAsync(career.Id, Ct);
        Assert.Equal(career.State.StateVersion, found!.State.StateVersion);
        Assert.Equal(Created, found.LastActionAt);
        Assert.Equal(Created.AddMonths(6), found.ExpiresAt);
    }

    [Fact]
    public async Task Replacing_credentials_while_at_a_version_fails_once_the_career_moved_on()
    {
        var (store, career) = await StoreWithCareerAsync();
        await store.AppendAsync(career.Id, career.State.StateVersion, Advanced(career.State), Entry(Guid.NewGuid()), Later, Ct);
        var newRecovery = SHA256.HashData("new recovery"u8);

        Assert.False(await store.ReplaceCredentialsAsync(career.Id, null, newRecovery, career.State.StateVersion, Later, Ct));
        Assert.Equal(career.RecoveryHash, (await store.FindCredentialsAsync(career.Id, Ct))!.RecoveryHash);
        Assert.True(await store.ReplaceCredentialsAsync(career.Id, null, newRecovery, career.State.StateVersion + 1, Later, Ct));
        Assert.Equal(newRecovery, (await store.FindCredentialsAsync(career.Id, Ct))!.RecoveryHash);
    }

    [Fact]
    public async Task Replacing_credentials_of_an_unknown_career_fails()
    {
        var store = await CreateStoreAsync();

        Assert.False(await store.ReplaceCredentialsAsync(Guid.NewGuid(), SHA256.HashData("x"u8), null, null, Later, Ct));
        Assert.Null(await store.FindCredentialsAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task An_action_moves_the_expiry_six_months_and_an_ending_twelve_months()
    {
        var (store, career) = await StoreWithCareerAsync();
        var v1 = Advanced(career.State);
        await store.AppendAsync(career.Id, career.State.StateVersion, v1, Entry(Guid.NewGuid()), Later, Ct);
        Assert.Equal(Later.AddMonths(6), (await store.FindAsync(career.Id, Ct))!.ExpiresAt);

        var ending = Later.AddDays(3);
        var ended = Advanced(v1) with { Status = CareerStatus.Ended };
        await store.AppendAsync(career.Id, v1.StateVersion, ended, Entry(Guid.NewGuid()), ending, Ct);

        var found = await store.FindAsync(career.Id, Ct);
        Assert.Equal(ending, found!.EndedAt);
        Assert.Equal(ending.AddMonths(12), found.ExpiresAt);
    }

    [Fact]
    public async Task A_rejected_append_keeps_the_expiry()
    {
        var (store, career) = await StoreWithCareerAsync();
        var command = Guid.NewGuid();
        var v1 = Advanced(career.State);
        await store.AppendAsync(career.Id, career.State.StateVersion, v1, Entry(command), Later, Ct);

        await store.AppendAsync(career.Id, v1.StateVersion, Advanced(v1), Entry(command), Later.AddDays(30), Ct);
        await store.AppendAsync(career.Id, career.State.StateVersion, v1, Entry(Guid.NewGuid()), Later.AddDays(30), Ct);

        Assert.Equal(Later.AddMonths(6), (await store.FindAsync(career.Id, Ct))!.ExpiresAt);
    }

    [Fact]
    public async Task Deleting_expired_careers_removes_them_with_their_log_and_keeps_the_others()
    {
        var store = await CreateStoreAsync();
        var expired = SampleCareer();
        var kept = SampleCareer(2) with { Id = Guid.CreateVersion7(Later), CreatedAt = Later };
        await store.CreateAsync(expired, Ct);
        await store.CreateAsync(kept, Ct);
        var command = Guid.NewGuid();
        await store.AppendAsync(expired.Id, expired.State.StateVersion, Advanced(expired.State), Entry(command), Created, Ct);

        Assert.Equal(1, await store.DeleteExpiredAsync(Created.AddMonths(6), 10, Ct));

        Assert.Null(await store.FindAsync(expired.Id, Ct));
        Assert.Null(await store.FindCredentialsAsync(expired.Id, Ct));
        Assert.Empty(await store.FindCommandAsync(expired.Id, command, Ct));
        Assert.NotNull(await store.FindAsync(kept.Id, Ct));
        Assert.Equal(0, await store.DeleteExpiredAsync(Created.AddMonths(6), 10, Ct));
    }

    [Fact]
    public async Task Deleting_expired_careers_stops_at_the_limit()
    {
        var store = await CreateStoreAsync();
        for (var i = 0; i < 3; i++)
        {
            await store.CreateAsync(SampleCareer((ulong)i + 1) with { Id = Guid.NewGuid() }, Ct);
        }

        Assert.Equal(2, await store.DeleteExpiredAsync(Later.AddMonths(6), 2, Ct));
        Assert.Equal(1, await store.DeleteExpiredAsync(Later.AddMonths(6), 2, Ct));
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
