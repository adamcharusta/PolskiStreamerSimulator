using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Support;
using PolskiStreamerSimulator.WebApp;

namespace PolskiStreamerSimulator.Tests.WebApp;

/// <summary>Spec M4-E over HTTP on SQL Server: the content workflow, its boundaries and the startup release.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AdminContentApiTests(SqlServerFixture sql)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Owner(HttpClient Client, string Csrf);

    private static async Task<Owner> SignInAsync(SqlApiFactory api)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var owner = (await OwnerCreateCommand.CreateAsync(users, "wlasciciel")).Credentials!;
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        await client.PostAsJsonAsync("/v1/admin/login", new { username = owner.Username, password = owner.Password }, Ct);
        var session = await (await client.PostAsJsonAsync("/v1/admin/login/code", new { code = Totp.Code(owner.AuthenticatorKey, DateTimeOffset.UtcNow) }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        return new Owner(client, session.GetProperty("csrfToken").GetString()!);
    }

    private static async Task<HttpResponseMessage> SendAsync(Owner owner, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", owner.Csrf);
        return await owner.Client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<int?> RankedSeasonAsync(SqlApiFactory api)
        => (await api.CreateClient().CreateCareerAsync(Ct, ranked: true)).Career.GetProperty("season").GetInt32();

    /// <summary>Puts the first event back with <paramref name="change"/> applied and its version bumped.</summary>
    private static async Task<JsonElement> EditFirstEventAsync(Owner owner, JsonElement draft, Action<JsonObject> change)
    {
        var item = JsonNode.Parse(draft.GetProperty("events")[0].GetProperty("event").GetRawText())!.AsObject();
        change(item);
        item["version"] = item["version"]!.GetValue<int>() + 1;
        var id = item["id"]!.GetValue<string>();
        return await JsonAsync(await SendAsync(owner, HttpMethod.Put, $"/v1/admin/content/draft/events/{id}", new { revision = draft.GetProperty("revision").GetInt32(), @event = item }));
    }

    [Fact]
    public async Task Startup_publishes_the_repository_content_once()
    {
        var connection = sql.NewDatabase();
        await using (var first = new SqlApiFactory(connection, TestPaths.ContentM3))
        {
            first.CreateClient();
        }

        await using var second = new SqlApiFactory(connection, TestPaths.ContentM3);
        var owner = await SignInAsync(second);

        var publications = await JsonAsync(await owner.Client.GetAsync("/v1/admin/content/publications", Ct));
        var only = Assert.Single(publications.EnumerateArray());
        Assert.Equal(("release", "deployment"), (only.GetProperty("kind").GetString(), only.GetProperty("actor").GetString()));
        await using var db = new GameDbContext(SqlServerFixture.Options(connection));
        Assert.NotNull((await db.Seasons.SingleAsync(Ct)).MechanicsHash);
    }

    [Fact]
    public async Task Content_endpoints_need_the_owner_and_the_anti_forgery_token()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase(), TestPaths.ContentM3);
        var visitor = api.CreateClient();
        var owner = await SignInAsync(api);

        var anonymous = await visitor.GetAsync("/v1/admin/content", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.True((await owner.Client.GetAsync("/v1/admin/content", Ct)).Headers.CacheControl!.NoStore);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync("/v1/admin/content/draft", new { }, Ct)).StatusCode);
        var put = await owner.Client.PutAsJsonAsync("/v1/admin/content/draft/balance", new { revision = 1, balance = new { } }, Ct);
        var delete = await owner.Client.DeleteAsync("/v1/admin/content/draft?revision=1", Ct);
        Assert.Equal((HttpStatusCode.BadRequest, "antiforgery-failed"), (put.StatusCode, await put.CodeAsync(Ct)));
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(owner, HttpMethod.Delete, "/v1/admin/content/draft?revision=1")).StatusCode);
    }

    [Fact]
    public async Task A_text_fix_keeps_the_season_and_a_mechanical_change_opens_the_next()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase(), TestPaths.ContentM3);
        var owner = await SignInAsync(api);
        Assert.Equal(1, await RankedSeasonAsync(api));

        var draft = await JsonAsync(await SendAsync(owner, HttpMethod.Post, "/v1/admin/content/draft", new { }));
        var fixedText = await EditFirstEventAsync(owner, draft, e => e["title"] = "Poprawiony tytuł");
        Assert.False(fixedText.GetProperty("diff").GetProperty("mechanical").GetBoolean());
        var text = await JsonAsync(await SendAsync(owner, HttpMethod.Post, "/v1/admin/content/publish", new { revision = fixedText.GetProperty("revision").GetInt32(), reason = "Literówka" }));
        Assert.Equal(1, await RankedSeasonAsync(api));

        draft = await JsonAsync(await SendAsync(owner, HttpMethod.Post, "/v1/admin/content/draft", new { }));
        var heavier = await EditFirstEventAsync(owner, draft, e => e["weight"] = e["weight"]!.GetValue<int>() + 2);
        Assert.True(heavier.GetProperty("diff").GetProperty("mechanical").GetBoolean());
        await JsonAsync(await SendAsync(owner, HttpMethod.Post, "/v1/admin/content/publish", new { revision = heavier.GetProperty("revision").GetInt32(), reason = "Częściej" }));
        Assert.Equal(2, await RankedSeasonAsync(api));

        var reverted = await JsonAsync(await SendAsync(owner, HttpMethod.Post, $"/v1/admin/content/publications/{text.GetProperty("number").GetInt32()}/revert", new { reason = "Wracamy" }));
        Assert.Equal(("revert", 4), (reverted.GetProperty("kind").GetString(), reverted.GetProperty("number").GetInt32()));
        Assert.Equal(3, await RankedSeasonAsync(api));

        var export = await JsonAsync(await owner.Client.GetAsync($"/v1/admin/content/publications/{reverted.GetProperty("number").GetInt32()}/export", Ct));
        Assert.Equal(text.GetProperty("digest").GetString(), export.GetProperty("digest").GetString());
        var audit = await JsonAsync(await owner.Client.GetAsync("/v1/admin/audit?limit=200", Ct));
        Assert.Contains(audit.EnumerateArray(), a => a.GetProperty("action").GetString() == "content.publish");
        Assert.Contains(audit.EnumerateArray(), a => a.GetProperty("action").GetString() == "content.revert");
    }

    [Fact]
    public async Task A_stale_or_invalid_publication_is_refused()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase(), TestPaths.ContentM3);
        var owner = await SignInAsync(api);
        var draft = await JsonAsync(await SendAsync(owner, HttpMethod.Post, "/v1/admin/content/draft", new { }));
        var broken = await EditFirstEventAsync(owner, draft, e => e["weight"] = -3);

        var invalid = await SendAsync(owner, HttpMethod.Post, "/v1/admin/content/publish", new { revision = broken.GetProperty("revision").GetInt32(), reason = "x" });
        var stale = await SendAsync(owner, HttpMethod.Post, "/v1/admin/content/publish", new { revision = 1, reason = "x" });
        var view = await JsonAsync(await owner.Client.GetAsync("/v1/admin/content", Ct));

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "content-invalid"), (invalid.StatusCode, await invalid.CodeAsync(Ct)));
        Assert.Equal((HttpStatusCode.Conflict, "draft-stale"), (stale.StatusCode, await stale.CodeAsync(Ct)));
        Assert.NotEqual(0, view.GetProperty("draft").GetProperty("problems").GetArrayLength());
        Assert.Equal(1, view.GetProperty("current").GetProperty("number").GetInt32());
    }
}
