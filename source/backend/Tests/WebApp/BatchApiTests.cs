using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

public sealed class BatchApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = api.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_batch_plays_thirteen_weeks_and_the_career_reads_back()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);

        var response = await client.PostBatchAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), weeks = 13 }, Ct);
        var batch = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var reread = await (await client.GetCareerAsync(id, secret, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(13, batch.GetProperty("weeksCommitted").GetInt32());
        Assert.Equal("completed", batch.GetProperty("stopReason").GetString());
        Assert.False(string.IsNullOrEmpty(batch.GetProperty("weeks")[0].GetProperty("planReason").GetProperty("code").GetString()));
        Assert.Equal(14, reread.GetProperty("week").GetInt32());
        Assert.Equal(13, reread.GetProperty("lastReport").GetProperty("week").GetInt32());
    }

    [Fact]
    public async Task Only_four_thirteen_or_fifty_two_weeks_are_accepted()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);

        var response = await client.PostBatchAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), weeks = 26 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation-failed", await response.CodeAsync(Ct));
    }

    [Fact]
    public async Task A_four_week_batch_with_a_strategy_runs_and_a_bad_strategy_is_refused()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var version = career.GetProperty("version").GetInt64();
        object Strategy(int restBelowEnergy) => new
        {
            categoryPlan = "strongest",
            focus = (string?)null,
            routine = new[] { "noAction", "trainCategory" },
            restBelowEnergy,
            restAboveBurnout = 70,
            reservePln = 0,
            upgrades = true,
            risk = "balanced",
            acceptOffers = true,
            wager = (string?)null,
            sideBusiness = false,
        };

        var bad = await client.PostBatchAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version, weeks = 4, strategy = Strategy(99) }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var ok = await client.PostBatchAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version, weeks = 4, strategy = Strategy(30) }, Ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task A_started_week_blocks_a_batch()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var started = await client.ActAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), type = "startWeek", mainAction = "rest", category = "fps" }, Ct);

        var response = await client.PostBatchAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = started.GetProperty("career").GetProperty("version").GetInt64(), weeks = 13 }, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("week-in-progress", await response.CodeAsync(Ct));
    }

    [Fact]
    public async Task Rejected_batches_return_their_stable_codes()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var version = career.GetProperty("version").GetInt64();
        var manual = Guid.NewGuid();
        await client.ActAsync(id, secret, new { commandId = manual, expectedVersion = version, type = "startWeek", mainAction = "rest", category = "fps" }, Ct);

        async Task<(HttpStatusCode Status, string? Code)> BatchAsync(string? withSecret, Guid commandId, long expectedVersion)
        {
            var response = await client.PostBatchAsync(id, withSecret, new { commandId, expectedVersion, weeks = 13 }, Ct);
            return (response.StatusCode, await response.CodeAsync(Ct));
        }

        Assert.Equal((HttpStatusCode.Unauthorized, "missing-secret"), await BatchAsync(null, Guid.NewGuid(), version + 1));
        Assert.Equal((HttpStatusCode.NotFound, "career-not-found"), await BatchAsync("not-the-secret", Guid.NewGuid(), version + 1));
        Assert.Equal((HttpStatusCode.Conflict, "stale-version"), await BatchAsync(secret, Guid.NewGuid(), version));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "command-id-reused"), await BatchAsync(secret, manual, version + 1));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"commandId":"6f1c2b7e-0000-4000-8000-000000000001","expectedVersion":0}""")]
    [InlineData("""{"commandId":"6f1c2b7e-0000-4000-8000-000000000001","expectedVersion":0,"weeks":"13"}""")]
    [InlineData("""{"expectedVersion":0,"weeks":13}""")]
    public async Task Malformed_batch_requests_return_validation_failed(string json)
    {
        var (id, secret, _) = await client.CreateCareerAsync(Ct);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/careers/{id}/batches") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {secret}");

        var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation-failed", await response.CodeAsync(Ct));
    }
}
