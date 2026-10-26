using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

/// <summary>The HTTP journey of an M3 career: a mandatory decision between weeks and the network odds.</summary>
public sealed class M3CareerApiTests(M3ApiFactory api) : IClassFixture<M3ApiFactory>
{
    private readonly HttpClient client = api.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_new_career_runs_the_m3_ruleset_and_shows_network_odds()
    {
        var (_, _, career) = await client.CreateCareerAsync(Ct);

        Assert.Equal("m3-systems.2", career.GetProperty("rulesetId").GetString());
        var network = career.GetProperty("next").GetProperty("plans").EnumerateArray()
            .First(p => p.GetProperty("plan").GetProperty("mainAction").GetString() == "networkCollaborate");
        Assert.InRange(network.GetProperty("odds").GetProperty("threshold").GetInt32(), 5, 95);
    }

    [Fact]
    public async Task A_decision_after_week_one_blocks_the_next_week_until_it_is_answered()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var none = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), type = "resolveDecision", choiceId = "steady" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, none.StatusCode);
        Assert.Equal("no-pending-decision", await none.CodeAsync(Ct));

        var settled = (await client.PlayWeekAsync(id, secret, career, Ct)).GetProperty("career");
        var version = settled.GetProperty("version").GetInt64();

        var next = settled.GetProperty("next");
        Assert.Equal("resolveDecision", next.GetProperty("kind").GetString());
        Assert.Equal("first-week", next.GetProperty("event").GetProperty("eventId").GetString());
        Assert.True(next.GetProperty("event").GetProperty("mandatory").GetBoolean());
        Assert.False(string.IsNullOrEmpty(next.GetProperty("event").GetProperty("text").GetString()));

        var early = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version, type = "startWeek", mainAction = "rest", category = "fps" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("decision-pending", await early.CodeAsync(Ct));

        var missingChoice = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version, type = "resolveDecision" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingChoice.StatusCode);

        var week2 = await client.PlayWeekAsync(id, secret, settled, Ct);
        Assert.Equal(2, week2.GetProperty("report").GetProperty("week").GetInt32());
        Assert.Equal("first-week", week2.GetProperty("report").GetProperty("decisionRolls")[0].GetProperty("eventId").GetString());

        var reread = await (await client.GetCareerAsync(id, secret, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(2, reread.GetProperty("lastReport").GetProperty("week").GetInt32());
    }

    [Fact]
    public async Task Respond_offer_round_trips_and_needs_an_answer()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var version = career.GetProperty("version").GetInt64();
        var missing = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version, type = "respondOffer", offerId = "x:1" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("validation-failed", await missing.CodeAsync(Ct));

        var unknown = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version, type = "respondOffer", offerId = "x:1", accept = true }, Ct);
        Assert.Equal("illegal-action", await unknown.CodeAsync(Ct));
        Assert.Equal("czatnia", career.GetProperty("platform").GetString());
        Assert.Equal(0, career.GetProperty("offers").GetArrayLength());
    }

    [Fact]
    public async Task History_lists_the_played_week_and_returns_its_report()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        await client.PlayWeekAsync(id, secret, career, Ct);

        using var history = new HttpRequestMessage(HttpMethod.Get, $"/v1/careers/{id}/history");
        history.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secret);
        var list = await (await client.SendAsync(history, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(1, list.GetProperty("weeks")[0].GetProperty("week").GetInt32());

        using var week = new HttpRequestMessage(HttpMethod.Get, $"/v1/careers/{id}/history/2");
        week.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secret);
        var missing = await client.SendAsync(week, Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("week-not-found", await missing.CodeAsync(Ct));
    }
}
