using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

/// <summary>Spec M4-A: repeat-safe creation, recovery, key rotation and rate limits over HTTP.</summary>
public sealed class AccessApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = api.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object Creation(Guid creationId, string nickname = "Streamer")
        => new { nickname, background = "dormRoomNewcomer", difficulty = "normal", pronouns = "woman", creationId };

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    [Fact]
    public async Task Creation_returns_a_recovery_key_and_the_expiry_date()
    {
        var body = await BodyAsync(await client.PostAsJsonAsync("/v1/careers", Creation(Guid.NewGuid()), Ct));

        var id = body.GetProperty("careerId").GetGuid();
        Assert.StartsWith($"pss1.{id:N}.", body.GetProperty("recoveryKey").GetString(), StringComparison.Ordinal);
        Assert.True(body.GetProperty("career").GetProperty("expiresAt").TryGetDateTimeOffset(out _));
    }

    [Fact]
    public async Task A_repeated_creation_returns_the_same_career_and_another_request_under_its_id_is_422()
    {
        var creationId = Guid.NewGuid();
        var first = await BodyAsync(await client.PostAsJsonAsync("/v1/careers", Creation(creationId), Ct));

        var repeat = await client.PostAsJsonAsync("/v1/careers", Creation(creationId), Ct);
        var reused = await client.PostAsJsonAsync("/v1/careers", Creation(creationId, "Other"), Ct);

        Assert.Equal(HttpStatusCode.Created, repeat.StatusCode);
        Assert.Equal(first.GetProperty("careerId").GetGuid(), (await BodyAsync(repeat)).GetProperty("careerId").GetGuid());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        Assert.Equal("creation-id-reused", (await BodyAsync(reused)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_empty_creation_id_is_a_validation_failure()
    {
        var response = await client.PostAsJsonAsync("/v1/careers", Creation(Guid.Empty), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recovery_returns_a_new_secret_and_the_old_one_stops_working()
    {
        var created = await BodyAsync(await client.PostAsJsonAsync("/v1/careers", Creation(Guid.NewGuid()), Ct));
        var id = created.GetProperty("careerId").GetGuid();

        var response = await client.PostAsJsonAsync("/v1/careers/recover", new { recoveryKey = created.GetProperty("recoveryKey").GetString() }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var recovered = await BodyAsync(response);
        Assert.Equal(id, recovered.GetProperty("careerId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await client.GetCareerAsync(id, recovered.GetProperty("secret").GetString(), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetCareerAsync(id, created.GetProperty("secret").GetString(), Ct)).StatusCode);
    }

    [Fact]
    public async Task A_wrong_key_is_404_recovery_failed_and_an_empty_one_is_400()
    {
        var wrong = await client.PostAsJsonAsync("/v1/careers/recover", new { recoveryKey = "pss1.nope" }, Ct);
        var empty = await client.PostAsJsonAsync("/v1/careers/recover", new { recoveryKey = "" }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        Assert.Equal("recovery-failed", (await BodyAsync(wrong)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task A_new_recovery_key_replaces_the_old_one()
    {
        var created = await BodyAsync(await client.PostAsJsonAsync("/v1/careers", Creation(Guid.NewGuid()), Ct));
        var id = created.GetProperty("careerId").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/careers/{id}/recovery-key");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.GetProperty("secret").GetString());

        var issued = await BodyAsync(await client.SendAsync(request, Ct));

        var oldKey = await client.PostAsJsonAsync("/v1/careers/recover", new { recoveryKey = created.GetProperty("recoveryKey").GetString() }, Ct);
        var newKey = await client.PostAsJsonAsync("/v1/careers/recover", new { recoveryKey = issued.GetProperty("recoveryKey").GetString() }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, oldKey.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newKey.StatusCode);
    }

    [Fact]
    public async Task A_new_recovery_key_needs_the_secret()
    {
        var response = await client.PostAsync($"/v1/careers/{Guid.NewGuid()}/recovery-key", null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Too_many_recoveries_are_429_rate_limited_problem_details()
    {
        using var limited = api.WithWebHostBuilder(b => b.UseSetting("RateLimits:RecoverPermits", "2"));
        var limitedClient = limited.CreateClient();

        for (var i = 0; i < 2; i++)
        {
            await limitedClient.PostAsJsonAsync("/v1/careers/recover", new { recoveryKey = "pss1.nope" }, Ct);
        }

        var rejected = await limitedClient.PostAsJsonAsync("/v1/careers/recover", new { recoveryKey = "pss1.nope" }, Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType!.MediaType);
        Assert.Equal("rate-limited", (await BodyAsync(rejected)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Too_many_creations_are_429_and_reads_are_not_limited()
    {
        using var limited = api.WithWebHostBuilder(b => b.UseSetting("RateLimits:CreatePermits", "1"));
        var limitedClient = limited.CreateClient();
        await limitedClient.PostAsJsonAsync("/v1/careers", Creation(Guid.NewGuid()), Ct);

        var rejected = await limitedClient.PostAsJsonAsync("/v1/careers", Creation(Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await limitedClient.GetAsync("/v1/career-options", Ct)).StatusCode);
    }
}
