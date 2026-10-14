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

    [Fact]
    public async Task Parallel_repeats_of_one_batch_store_thirteen_weeks_and_answer_alike()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = sql.NewDatabase();
        await using var api = new SqlApiFactory(connection);
        var client = api.CreateClient();
        var (id, secret, career) = await client.CreateCareerAsync(ct);
        var body = new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), weeks = 13 };

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostBatchAsync(id, secret, body, ct)));
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync(ct)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Single(bodies.Distinct(StringComparer.Ordinal));
        Assert.Contains("\"stopReason\":\"completed\"", bodies[0], StringComparison.Ordinal);
        await using var db = new GameDbContext(SqlServerFixture.Options(connection));
        Assert.Equal(13, await db.CareerActions.CountAsync(a => a.CareerId == id, ct));
    }

    [Fact]
    public async Task A_manual_week_and_a_batch_survive_a_restart_and_repeat_alike()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = sql.NewDatabase();
        Guid id;
        string secret;
        object body;
        string batch;
        string before;
        await using (var first = new SqlApiFactory(connection))
        {
            var client = first.CreateClient();
            var created = await client.CreateCareerAsync(ct);
            (id, secret) = (created.Id, created.Secret);
            var week = await client.PlayWeekAsync(id, secret, created.Career, ct);
            body = new { commandId = Guid.NewGuid(), expectedVersion = week.GetProperty("career").GetProperty("version").GetInt64(), weeks = 13 };
            batch = await (await client.PostBatchAsync(id, secret, body, ct)).Content.ReadAsStringAsync(ct);
            before = await (await client.GetCareerAsync(id, secret, ct)).Content.ReadAsStringAsync(ct);
        }

        await using var second = new SqlApiFactory(connection);
        var again = second.CreateClient();
        var after = await (await again.GetCareerAsync(id, secret, ct)).Content.ReadAsStringAsync(ct);
        var repeat = await (await again.PostBatchAsync(id, secret, body, ct)).Content.ReadAsStringAsync(ct);

        Assert.Contains("\"stopReason\":\"completed\"", batch, StringComparison.Ordinal);
        Assert.Contains("\"lastSettledWeek\":14", before, StringComparison.Ordinal);
        Assert.Equal(before, after);
        Assert.Equal(batch, repeat);
    }
}
