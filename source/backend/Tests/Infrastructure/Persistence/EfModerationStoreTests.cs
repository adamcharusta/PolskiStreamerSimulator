using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Moderation;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Application.Leaderboard;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure.Persistence;

/// <summary>Spec M4-D §5 on SQL Server: a change and its audit record commit together; a no-op writes nothing.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EfModerationStoreTests(SqlServerFixture sql)
{
    private static readonly DateTimeOffset At = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AuditEntry Audit(string action, string reason) => new("owner", action, "entry", reason, null, null, At);

    private async Task<(TestDbContextFactory Factory, EfModerationStore Store, Guid EntryId)> PublishedAsync()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);
        await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(new TestCurrentContent(TestPaths.ContentM1).Files, Ct);
        await new EfSeasonStore(factory).EnsureCurrentAsync("m1", TestContent.Of(TestPaths.ContentM1).Digest, At, Ct);
        var entry = LeaderboardRulesTests.Entry(Guid.NewGuid(), 100, 1, At);
        await new EfLeaderboardStore(factory).AddAsync(entry, Ct);
        return (factory, new EfModerationStore(factory), entry.Id);
    }

    [Fact]
    public async Task A_hide_and_a_restore_each_store_their_audit_record()
    {
        var (factory, store, entryId) = await PublishedAsync();

        var (hid, hidden) = await store.SetHiddenAsync(entryId, true, Audit(AuditActions.Hide, "Spam"), Ct);
        var (restored, visible) = await store.SetHiddenAsync(entryId, false, Audit(AuditActions.Restore, "Pomyłka"), Ct);

        Assert.Equal((ModerationOutcome.Changed, At, "Spam"), (hid, hidden!.HiddenAt, hidden.HiddenReason));
        Assert.Equal((ModerationOutcome.Changed, null, null), (restored, visible!.HiddenAt, visible.HiddenReason));
        var audit = await store.RecentAsync(10, Ct);
        Assert.Equal(["Pomyłka", "Spam"], audit.Select(a => a.Entry.Reason));
        Assert.Contains("\"hiddenReason\":\"Spam\"", audit[1].Entry.NewJson, StringComparison.Ordinal);
        Assert.Contains("\"hiddenReason\":\"Spam\"", audit[0].Entry.OldJson, StringComparison.Ordinal);
        Assert.Single(await new EfLeaderboardStore(factory).TopAsync(1, 100, Ct));
    }

    [Fact]
    public async Task A_change_that_does_nothing_writes_no_audit_record()
    {
        var (factory, store, entryId) = await PublishedAsync();

        var (outcome, _) = await store.SetHiddenAsync(entryId, false, Audit(AuditActions.Restore, "nic"), Ct);
        var (missing, _) = await store.SetHiddenAsync(Guid.NewGuid(), true, Audit(AuditActions.Hide, "nic"), Ct);

        Assert.Equal((ModerationOutcome.Unchanged, ModerationOutcome.NotFound), (outcome, missing));
        await using var db = factory.CreateDbContext();
        Assert.Equal(0, await db.AuditRecords.CountAsync(Ct));
    }

    [Fact]
    public async Task Concurrent_hides_change_the_entry_once()
    {
        var (_, store, entryId) = await PublishedAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => store.SetHiddenAsync(entryId, true, Audit(AuditActions.Hide, $"r{i}"), Ct)));

        Assert.Single(outcomes, o => o.Outcome == ModerationOutcome.Changed);
        Assert.Single(await store.RecentAsync(10, Ct));
    }

    [Fact]
    public async Task A_season_extension_stores_its_audit_record()
    {
        var (_, store, _) = await PublishedAsync();
        var factory = await sql.MigratedDatabaseAsync(Ct);
        await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(new TestCurrentContent(TestPaths.ContentM1).Files, Ct);
        var seasons = new EfSeasonStore(factory);
        await seasons.EnsureCurrentAsync("m1", TestContent.Of(TestPaths.ContentM1).Digest, At, Ct);

        await seasons.AddExtensionAsync(1, At.AddDays(200), new AuditEntry("command", AuditActions.Extend, "1", "Awaria", "{}", "{}", At), Ct);

        var record = Assert.Single(await new EfModerationStore(factory).RecentAsync(10, Ct));
        Assert.Equal(("command", AuditActions.Extend, "Awaria"), (record.Entry.Actor, record.Entry.Action, record.Entry.Reason));
        Assert.Equal(At.AddDays(200), (await seasons.FindAsync(1, Ct))!.ExtendedUntil);
        Assert.Empty(await store.RecentAsync(10, Ct));
    }
}
