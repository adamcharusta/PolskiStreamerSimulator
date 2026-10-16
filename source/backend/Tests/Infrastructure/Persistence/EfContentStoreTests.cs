using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EfContentStoreTests(SqlServerFixture sql)
{
    private static readonly ContentFiles Files = new(TestContentFiles.Balance, TestContentFiles.Events);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stored_files_load_back_as_the_same_snapshot()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);
        var stored = await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(Files, Ct);

        var loaded = await new EfContentStore(factory, TimeProvider.System).FindAsync(stored.Digest, Ct);

        Assert.Equal(TestContent.Valid().Digest, stored.Digest);
        Assert.Equal(stored.Digest, loaded!.Digest);
        Assert.Equal(stored.Events.Select(e => e.Id), loaded.Events.Select(e => e.Id));
    }

    [Fact]
    public async Task Storing_the_same_files_twice_keeps_one_row()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);
        await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(Files, Ct);
        await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(Files, Ct);

        await using var db = factory.CreateDbContext();
        Assert.Equal(1, await db.ContentSnapshots.CountAsync(Ct));
    }

    [Fact]
    public async Task An_older_snapshot_is_still_found_after_a_newer_one_is_stored()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);
        var old = await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(Files, Ct);
        var newer = new ContentFiles([.. TestContentFiles.Balance, .. "\n"u8.ToArray()], TestContentFiles.Events);
        var current = await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(newer, Ct);

        var reloaded = await new EfContentStore(factory, TimeProvider.System).FindAsync(old.Digest, Ct);

        Assert.NotEqual(old.Digest, current.Digest);
        Assert.Equal(old.Digest, reloaded!.Digest);
    }

    [Fact]
    public async Task An_unknown_digest_is_not_found()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);

        Assert.Null(await new EfContentStore(factory, TimeProvider.System).FindAsync(new string('0', 64), Ct));
    }

    [Fact]
    public async Task A_changed_event_under_the_same_version_is_refused()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);
        var store = new EfContentStore(factory, TimeProvider.System);
        await store.EnsureStoredAsync(new ContentFiles(TestContentFiles.Balance, TestContentFiles.SchemaTwoEvents()), Ct);

        var changed = new ContentFiles(TestContentFiles.Balance, TestContentFiles.SchemaTwoEvents(" Zmieniono."));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.EnsureStoredAsync(changed, Ct));

        Assert.Contains("event-version-changed:", error.Message, StringComparison.Ordinal);
        await using var db = factory.CreateDbContext();
        Assert.Equal(1, await db.ContentSnapshots.CountAsync(Ct));
    }
}
