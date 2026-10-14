using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>HTTP helpers for the career API.</summary>
public static class ApiCalls
{
    public static async Task<(Guid Id, string Secret, JsonElement Career)> CreateCareerAsync(this HttpClient client, CancellationToken ct, string nickname = "Streamer")
    {
        var response = await client.PostAsJsonAsync("/v1/careers", new { nickname, background = "dormRoomNewcomer", difficulty = "normal", pronouns = "woman" }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return (body.GetProperty("careerId").GetGuid(), body.GetProperty("secret").GetString()!, body.GetProperty("career"));
    }

    public static Task<HttpResponseMessage> GetCareerAsync(this HttpClient client, Guid id, string? secret, CancellationToken ct)
        => client.SendAsync(Request(HttpMethod.Get, $"/v1/careers/{id}", secret, null), ct);

    public static Task<HttpResponseMessage> PostActionAsync(this HttpClient client, Guid id, string? secret, object body, CancellationToken ct)
        => client.SendAsync(Request(HttpMethod.Post, $"/v1/careers/{id}/actions", secret, body), ct);

    public static Task<HttpResponseMessage> PostBatchAsync(this HttpClient client, Guid id, string secret, object body, CancellationToken ct)
        => client.SendAsync(Request(HttpMethod.Post, $"/v1/careers/{id}/batches", secret, body), ct);

    public static async Task<JsonElement> ActAsync(this HttpClient client, Guid id, string secret, object body, CancellationToken ct)
    {
        var response = await client.PostActionAsync(id, secret, body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    /// <summary>Plays one manual week: the first enabled plan, the first choice of every event, then settlement.</summary>
    public static async Task<JsonElement> PlayWeekAsync(this HttpClient client, Guid id, string secret, JsonElement career, CancellationToken ct)
    {
        var plan = career.GetProperty("next").GetProperty("plans").EnumerateArray()
            .First(p => p.GetProperty("disabledReason").ValueKind == JsonValueKind.Null)
            .GetProperty("plan");
        var outcome = await client.ActAsync(id, secret, new
        {
            commandId = Guid.NewGuid(),
            expectedVersion = career.GetProperty("version").GetInt64(),
            type = "startWeek",
            mainAction = plan.GetProperty("mainAction").GetString(),
            category = plan.GetProperty("category").GetString(),
            trainTarget = Text(plan, "trainTarget"),
            upgradeSlot = Text(plan, "upgradeSlot"),
        }, ct);
        var view = outcome.GetProperty("career");
        while (view.GetProperty("next").GetProperty("kind").GetString() == "resolveEvent")
        {
            var choiceId = view.GetProperty("next").GetProperty("event").GetProperty("choices")[0].GetProperty("choiceId").GetString();
            outcome = await client.ActAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = view.GetProperty("version").GetInt64(), type = "resolveEvent", choiceId }, ct);
            view = outcome.GetProperty("career");
        }

        return await client.ActAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = view.GetProperty("version").GetInt64(), type = "settleWeek" }, ct);
    }

    public static async Task<string?> CodeAsync(this HttpResponseMessage response, CancellationToken ct)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("code").GetString();

    private static string? Text(JsonElement element, string name)
        => element.GetProperty(name).ValueKind == JsonValueKind.Null ? null : element.GetProperty(name).GetString();

    private static HttpRequestMessage Request(HttpMethod method, string uri, string? secret, object? body)
    {
        var request = new HttpRequestMessage(method, uri);
        if (secret is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
