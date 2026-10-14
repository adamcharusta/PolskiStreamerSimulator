using System.Net;
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class BatchApiSqlTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_batch_stores_thirteen_consecutive_weeks()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = sql.NewDatabase();
        await using var api = new SqlApiFactory(connection);
        var client = api.CreateClient();
        var (id, secret, career) = await client.CreateCareerAsync(ct);

        var response = await client.PostBatchAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), weeks = 13 }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = new GameDbContext(SqlServerFixture.Options(connection));
        var log = await db.CareerActions.Where(a => a.CareerId == id).OrderBy(a => a.Sequence).ToListAsync(ct);
        Assert.Equal(Enumerable.Range(1, 13), log.Select(a => a.Sequence));
        Assert.All(log, a => Assert.Equal(ActionKind.AutoWeek, a.Kind));
        Assert.True(log.Zip(log.Skip(1)).All(p => p.First.StateVersionAfter < p.Second.StateVersionAfter));
        var stored = await db.Careers.SingleAsync(c => c.Id == id, ct);
        Assert.Equal(log[^1].StateVersionAfter, stored.StateVersion);
        Assert.Equal(log[^1].StateHashAfter, stored.StateHash);
    }
}
