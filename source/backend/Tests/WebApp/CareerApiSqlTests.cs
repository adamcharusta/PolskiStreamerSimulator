using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CareerApiSqlTests(SqlServerFixture sql)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<int> LogCountAsync(string connection, Guid careerId)
    {
        await using var db = new GameDbContext(SqlServerFixture.Options(connection));
        return await db.CareerActions.CountAsync(a => a.CareerId == careerId, Ct);
    }

    [Fact]
    public async Task A_played_week_survives_an_api_restart()
    {
        var connection = sql.NewDatabase();
        Guid id;
        string secret;
        string before;
        await using (var first = new SqlApiFactory(connection))
        {
            var client = first.CreateClient();
            var created = await client.CreateCareerAsync(Ct);
            (id, secret) = (created.Id, created.Secret);
            await client.PlayWeekAsync(id, secret, created.Career, Ct);
            before = await (await client.GetCareerAsync(id, secret, Ct)).Content.ReadAsStringAsync(Ct);
        }

        await using var second = new SqlApiFactory(connection);
        var after = await (await second.CreateClient().GetCareerAsync(id, secret, Ct)).Content.ReadAsStringAsync(Ct);

        Assert.Equal(before, after);
        Assert.Contains("\"lastReport\":{", after, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Parallel_repeats_of_one_command_store_one_entry_and_answer_alike()
    {
        var connection = sql.NewDatabase();
        await using var api = new SqlApiFactory(connection);
        var client = api.CreateClient();
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var body = new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), type = "startWeek", mainAction = "rest", category = "fps" };

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostActionAsync(id, secret, body, Ct)));
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync(Ct)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Single(bodies.Distinct(StringComparer.Ordinal));
        Assert.Equal(1, await LogCountAsync(connection, id));
    }

    [Fact]
    public async Task Parallel_commands_at_one_version_let_exactly_one_win()
    {
        var connection = sql.NewDatabase();
        await using var api = new SqlApiFactory(connection);
        var client = api.CreateClient();
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var version = career.GetProperty("version").GetInt64();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostActionAsync(
            id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version, type = "startWeek", mainAction = "rest", category = "fps" }, Ct)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
            Assert.Equal("stale-version", await loser.CodeAsync(Ct));
        }

        Assert.Equal(1, await LogCountAsync(connection, id));
    }

    [Fact]
    public async Task A_secret_opens_only_its_own_career()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase());
        var client = api.CreateClient();
        var mine = await client.CreateCareerAsync(Ct);
        var theirs = await client.CreateCareerAsync(Ct);

        var response = await client.GetCareerAsync(mine.Id, theirs.Secret, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("career-not-found", body.GetProperty("code").GetString());
    }
}
