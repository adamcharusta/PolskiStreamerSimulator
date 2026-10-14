using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DatabaseInitializerTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Startup_migrates_the_database_and_stores_the_current_content()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = sql.NewDatabase();
        await using (var api = new SqlApiFactory(connection))
        {
            Assert.Equal("ok", await api.CreateClient().GetStringAsync("/health", ct));
        }

        await using var db = new GameDbContext(SqlServerFixture.Options(connection));
        Assert.Equal([TestContent.Valid().Digest], await db.ContentSnapshots.Select(s => s.Digest).ToListAsync(ct));
    }
}
