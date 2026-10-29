using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

/// <summary>Spec M4-B: ranked creation, publication and the public tables over HTTP.</summary>
public sealed class LeaderboardApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = api.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private async Task<(Guid Id, string Secret, JsonElement Career)> RetiredAsync(bool ranked = true)
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct, ranked: ranked);
        var outcome = await client.ActAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), type = "retire" }, Ct);
        return (id, secret, outcome.GetProperty("career"));
    }

    [Fact]
    public async Task A_ranked_career_carries_its_season()
    {
        var (_, _, career) = await client.CreateCareerAsync(Ct, ranked: true);

        Assert.True(career.GetProperty("ranked").GetBoolean());
        Assert.Equal(1, career.GetProperty("season").GetInt32());
    }

    [Fact]
    public async Task Publication_ignores_a_client_score_and_a_repeat_returns_the_one_entry()
    {
        var (id, secret, career) = await RetiredAsync();
        var score = career.GetProperty("score").GetProperty("finalScore").GetInt64();

        var first = await client.PostLeaderboardEntryAsync(id, secret, new { nickname = "ab", score = 999_999_999, weeksPlayed = 0 }, Ct);
        var repeat = await client.PostLeaderboardEntryAsync(id, secret, new { nickname = "zz" }, Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var entry = (await BodyAsync(first)).GetProperty("entry");
        Assert.Equal("AB", entry.GetProperty("nickname").GetString());
        Assert.Equal(score, entry.GetProperty("score").GetInt64());
        Assert.Equal("AB", (await BodyAsync(repeat)).GetProperty("entry").GetProperty("nickname").GetString());
        Assert.NotNull(await api.Entries.FindByCareerAsync(id, Ct));
        Assert.Equal("published", (await BodyAsync(await client.GetLeaderboardEntryAsync(id, secret, Ct))).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Publication_errors_have_their_codes()
    {
        var (unrankedId, unrankedSecret, _) = await RetiredAsync(ranked: false);
        var (runningId, runningSecret, _) = await client.CreateCareerAsync(Ct, ranked: true);
        var (rankedId, rankedSecret, _) = await RetiredAsync();

        var unranked = await client.PostLeaderboardEntryAsync(unrankedId, unrankedSecret, new { nickname = "AAA" }, Ct);
        var running = await client.PostLeaderboardEntryAsync(runningId, runningSecret, new { nickname = "AAA" }, Ct);
        var invalid = await client.PostLeaderboardEntryAsync(rankedId, rankedSecret, new { nickname = "<b>" }, Ct);
        var missing = await client.PostLeaderboardEntryAsync(rankedId, null, new { nickname = "AAA" }, Ct);

        Assert.Equal((HttpStatusCode.Conflict, "career-unranked"), (unranked.StatusCode, (await BodyAsync(unranked)).GetProperty("code").GetString()));
        Assert.Equal((HttpStatusCode.Conflict, "career-not-ended"), (running.StatusCode, (await BodyAsync(running)).GetProperty("code").GetString()));
        Assert.Equal((HttpStatusCode.BadRequest, "validation-failed"), (invalid.StatusCode, (await BodyAsync(invalid)).GetProperty("code").GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
    }

    [Fact]
    public async Task The_public_table_exposes_only_public_fields_and_may_be_cached()
    {
        var (id, secret, _) = await RetiredAsync();
        await client.PostLeaderboardEntryAsync(id, secret, new { nickname = "PUB" }, Ct);

        var response = await client.GetAsync("/v1/leaderboard?season=1", Ct);

        Assert.Equal("public, max-age=30", response.Headers.CacheControl?.ToString());
        var body = await BodyAsync(response);
        Assert.Equal(1, body.GetProperty("season").GetProperty("number").GetInt32());
        var row = body.GetProperty("entries").EnumerateArray().Single(e => e.GetProperty("nickname").GetString() == "PUB");
        Assert.Equal(
            ["difficulty", "ending", "nickname", "rank", "score", "submittedAt", "weeksPlayed"],
            row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(id.ToString(), await response.Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_unknown_season_is_404_and_the_seasons_are_listed()
    {
        var unknown = await client.GetAsync("/v1/leaderboard?season=99", Ct);
        var seasons = await BodyAsync(await client.GetAsync("/v1/seasons", Ct));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("season-not-found", (await BodyAsync(unknown)).GetProperty("code").GetString());
        var current = Assert.Single(seasons.EnumerateArray());
        Assert.True(current.GetProperty("current").GetBoolean());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("submissionDeadline").ValueKind);
    }
}
