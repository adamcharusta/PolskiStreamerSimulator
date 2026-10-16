using Microsoft.EntityFrameworkCore;
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
}
