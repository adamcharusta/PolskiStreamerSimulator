using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MigrationTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Migrations_build_the_schema_on_an_empty_database()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = await sql.MigratedDatabaseAsync(ct);
        await using var db = factory.CreateDbContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(ct));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(0, await db.Careers.CountAsync(ct));
        Assert.Equal(0, await db.CareerActions.CountAsync(ct));
        Assert.Equal(0, await db.ContentSnapshots.CountAsync(ct));
    }

    [Fact]
    public async Task The_M4a_migration_dates_the_expiry_of_careers_stored_before_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = new TestDbContextFactory(SqlServerFixture.Options(sql.NewDatabase()));
        await using var db = factory.CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261014003156_InitialGameSchema", cancellationToken: ct);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [game].[content_snapshots] ([digest], [balance_json], [events_json], [created_at])
            VALUES (REPLICATE('a', 64), 0x00, 0x00, '2026-01-01T00:00:00+00:00');
            INSERT INTO [game].[careers] ([id], [nickname], [ruleset_id], [content_digest], [ranked], [status], [state_version], [state_json], [state_hash], [created_at], [last_action_at])
            VALUES ('00000000-0000-0000-0000-000000000001', 'Active', 'm1', REPLICATE('a', 64), 0, 'Active', 3, '{{}}', REPLICATE('b', 64), '2026-01-01T00:00:00+00:00', '2026-08-31T10:00:00+00:00'),
                   ('00000000-0000-0000-0000-000000000002', 'Ended', 'm1', REPLICATE('a', 64), 0, 'Ended', 9, '{{}}', REPLICATE('b', 64), '2026-01-01T00:00:00+00:00', '2026-03-15T10:00:00+00:00');
            """,
            ct);

        await migrator.MigrateAsync(cancellationToken: ct);

        var careers = await db.Careers.AsNoTracking().OrderBy(c => c.Nickname).ToListAsync(ct);
        var active = careers[0];
        var ended = careers[1];
        Assert.Null(active.EndedAt);
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero).AddMonths(6), active.ExpiresAt);
        Assert.Equal(new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero), ended.EndedAt);
        Assert.Equal(new DateTimeOffset(2027, 3, 15, 10, 0, 0, TimeSpan.Zero), ended.ExpiresAt);

        // M4-B: careers stored before it stay unranked and belong to no season.
        Assert.All(careers, c => Assert.False(c.Ranked));
        Assert.All(careers, c => Assert.Null(c.SeasonId));
        Assert.Equal(0, await db.Seasons.CountAsync(ct));

        // M4-C: careers stored before it have no slots.
        Assert.Equal(0, await db.SaveSlots.CountAsync(ct));
    }
}
