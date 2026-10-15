using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

public sealed class CareerApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = api.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creating_a_career_returns_201_with_its_location_secret_and_first_plan()
    {
        var response = await client.PostAsJsonAsync("/v1/careers", new { nickname = "Streamer", background = "dormRoomNewcomer", difficulty = "normal", pronouns = "woman" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = body.GetProperty("careerId").GetGuid();
        Assert.Equal($"/v1/careers/{id}", response.Headers.Location!.OriginalString);
        Assert.Equal(43, body.GetProperty("secret").GetString()!.Length);
        Assert.Equal("planWeek", body.GetProperty("career").GetProperty("next").GetProperty("kind").GetString());
        Assert.Equal("dormRoomNewcomer", body.GetProperty("career").GetProperty("background").GetString());
    }

    [Fact]
    public async Task Career_options_need_no_secret()
    {
        var response = await client.GetAsync("/v1/career-options", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("dormRoomNewcomer", body.GetProperty("backgrounds")[0].GetProperty("background").GetString());
        Assert.Equal(1.1m, body.GetProperty("difficulties")[1].GetProperty("scoreMultiplier").GetDecimal());
        Assert.Equal("nonbinary", body.GetProperty("pronouns")[2].GetString());
    }

    [Fact]
    public async Task An_invalid_nickname_returns_400_validation_failed_with_field_errors()
    {
        var response = await client.PostAsJsonAsync("/v1/careers", new { nickname = "x", background = "dormRoomNewcomer", difficulty = "normal", pronouns = "woman" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation-failed", body.GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"nickname":"Streamer","background":"wizard","difficulty":"normal","pronouns":"woman"}""")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{"nickname":"Streamer","difficulty":"normal","pronouns":"woman"}""")]
    [InlineData("""{"nickname":"Streamer","background":0,"difficulty":"normal","pronouns":"woman"}""")]
    public async Task Malformed_json_and_unknown_enums_return_validation_failed(string json)
    {
        var response = await client.PostAsync("/v1/careers", new StringContent(json, Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation-failed", await response.CodeAsync(Ct));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"commandId":"6f1c2b7e-0000-4000-8000-000000000001","expectedVersion":0,"type":"startWeek","mainAction":"rest","category":"fps","trainTarget":99}""")]
    [InlineData("""{"commandId":"6f1c2b7e-0000-4000-8000-000000000001","expectedVersion":0,"type":"startWeek","mainAction":"rest","category":"fps","upgradeSlot":"7"}""")]
    [InlineData("""{"commandId":"6f1c2b7e-0000-4000-8000-000000000001","expectedVersion":0,"mainAction":"rest","category":"fps"}""")]
    [InlineData("""{"commandId":"6f1c2b7e-0000-4000-8000-000000000001","type":"settleWeek"}""")]
    [InlineData("""{"commandId":"6f1c2b7e-0000-4000-8000-000000000001","expectedVersion":"0","type":"settleWeek"}""")]
    public async Task Malformed_action_requests_return_validation_failed(string json)
    {
        var (id, secret, _) = await client.CreateCareerAsync(Ct);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/careers/{id}/actions") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {secret}");

        var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation-failed", await response.CodeAsync(Ct));
    }

    [Fact]
    public async Task Rejected_steps_return_their_stable_codes()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var version = career.GetProperty("version").GetInt64();
        var command = Guid.NewGuid();

        var illegal = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version, type = "startWeek", mainAction = "improveProduction", category = "fps" }, Ct);
        var started = await client.ActAsync(id, secret, new { commandId = command, expectedVersion = version, type = "startWeek", mainAction = "rest", category = "fps" }, Ct);
        var startedVersion = started.GetProperty("career").GetProperty("version").GetInt64();
        var unknownChoice = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = startedVersion, type = "resolveEvent", choiceId = "no-such-choice" }, Ct);
        var reused = await client.PostActionAsync(id, secret, new { commandId = command, expectedVersion = startedVersion, type = "settleWeek" }, Ct);

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "illegal-action"), (illegal.StatusCode, await illegal.CodeAsync(Ct)));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "unknown-choice"), (unknownChoice.StatusCode, await unknownChoice.CodeAsync(Ct)));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "command-id-reused"), (reused.StatusCode, await reused.CodeAsync(Ct)));
    }

    [Fact]
    public async Task An_ended_career_returns_409_career_ended()
    {
        var (id, secret, _) = await client.CreateCareerAsync(Ct);
        var state = (await api.Store.FindAsync(id, Ct))!.State;
        var ended = state with { StateVersion = state.StateVersion + 1, Status = CareerStatus.Ended, Ending = EndingReason.EarlyRetirement };
        await api.Store.AppendAsync(id, state.StateVersion, ended, new NewAction(Guid.NewGuid(), 0, ActionKind.SettleWeek, "{}", "{}", null), DateTimeOffset.UnixEpoch, Ct);

        var response = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = ended.StateVersion, type = "startWeek", mainAction = "rest", category = "fps" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("career-ended", await response.CodeAsync(Ct));
    }

    [Fact]
    public async Task Reading_without_a_secret_returns_401_missing_secret()
    {
        var (id, _, _) = await client.CreateCareerAsync(Ct);

        var response = await client.GetCareerAsync(id, null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("missing-secret", await response.CodeAsync(Ct));
    }

    [Fact]
    public async Task Another_careers_secret_and_an_unknown_id_both_return_404()
    {
        var mine = await client.CreateCareerAsync(Ct);
        var theirs = await client.CreateCareerAsync(Ct);

        var wrongSecret = await client.GetCareerAsync(mine.Id, theirs.Secret, Ct);
        var unknown = await client.GetCareerAsync(Guid.NewGuid(), theirs.Secret, Ct);

        Assert.Equal(HttpStatusCode.NotFound, wrongSecret.StatusCode);
        Assert.Equal("career-not-found", await wrongSecret.CodeAsync(Ct));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("career-not-found", await unknown.CodeAsync(Ct));
    }

    [Fact]
    public async Task The_bearer_scheme_is_case_insensitive()
    {
        var (id, secret, _) = await client.CreateCareerAsync(Ct);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/careers/{id}");
        request.Headers.TryAddWithoutValidation("Authorization", $"bearer {secret}");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request, Ct)).StatusCode);
    }

    [Fact]
    public async Task A_week_can_be_played_and_read_back()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);

        var settled = await client.PlayWeekAsync(id, secret, career, Ct);
        var reread = await (await client.GetCareerAsync(id, secret, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(1, settled.GetProperty("report").GetProperty("week").GetInt32());
        Assert.Equal(1, reread.GetProperty("lastReport").GetProperty("week").GetInt32());
        Assert.Equal(settled.GetProperty("career").GetProperty("version").GetInt64(), reread.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Rule_rejections_map_to_their_status_codes()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var version = career.GetProperty("version").GetInt64();

        var stale = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version + 5, type = "settleWeek" }, Ct);
        var noWeek = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version, type = "resolveEvent", choiceId = "x" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("stale-version", await stale.CodeAsync(Ct));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noWeek.StatusCode);
        Assert.Equal("no-pending-week", await noWeek.CodeAsync(Ct));
    }

    [Fact]
    public async Task A_repeated_request_returns_the_same_body()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var body = new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), type = "startWeek", mainAction = "rest", category = "fps" };

        var first = await (await client.PostActionAsync(id, secret, body, Ct)).Content.ReadAsStringAsync(Ct);
        var second = await (await client.PostActionAsync(id, secret, body, Ct)).Content.ReadAsStringAsync(Ct);

        Assert.Equal(first, second);
    }
}
