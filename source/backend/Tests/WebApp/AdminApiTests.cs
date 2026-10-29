using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Infrastructure.Security;
using PolskiStreamerSimulator.Tests.Support;
using PolskiStreamerSimulator.WebApp;

namespace PolskiStreamerSimulator.Tests.WebApp;

/// <summary>Spec M4-D §3–§6 over HTTP on SQL Server: the owner's sign-in, its boundaries and moderation.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AdminApiTests(SqlServerFixture sql)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] OwnerGets = ["/v1/admin/session", "/v1/admin/leaderboard", "/v1/admin/audit"];

    private static readonly string[] OwnerPosts =
    [
        "/v1/admin/logout",
        $"/v1/admin/leaderboard/{Guid.Empty}/hide",
        $"/v1/admin/leaderboard/{Guid.Empty}/restore",
        "/v1/admin/seasons/1/extension",
    ];

    private static HttpClient Browser(SqlApiFactory api)
        => api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    private static async Task<OwnerCredentials> OwnerAsync(SqlApiFactory api)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        return (await OwnerCreateCommand.CreateAsync(users, "wlasciciel")).Credentials!;
    }

    private static async Task<JsonElement> SignInAsync(HttpClient client, OwnerCredentials owner)
    {
        var password = await client.PostAsJsonAsync("/v1/admin/login", new { username = owner.Username, password = owner.Password }, Ct);
        Assert.Equal(HttpStatusCode.OK, password.StatusCode);
        var code = await client.PostAsJsonAsync("/v1/admin/login/code", new { code = Totp.Code(owner.AuthenticatorKey, DateTimeOffset.UtcNow) }, Ct);
        Assert.Equal(HttpStatusCode.OK, code.StatusCode);
        return await code.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, string? csrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (csrf is not null)
        {
            request.Headers.Add("X-CSRF-TOKEN", csrf);
        }

        return client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task Every_admin_endpoint_rejects_a_visitor_and_a_half_signed_in_owner()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase());
        var owner = await OwnerAsync(api);
        var visitor = Browser(api);
        var halfway = Browser(api);
        await halfway.PostAsJsonAsync("/v1/admin/login", new { username = owner.Username, password = owner.Password }, Ct);

        foreach (var client in new[] { visitor, halfway })
        {
            foreach (var path in OwnerGets)
            {
                var response = await client.GetAsync(path, Ct);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.Equal("owner-unauthorized", await response.CodeAsync(Ct));
            }

            foreach (var path in OwnerPosts)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(client, path, new { reason = "x", until = DateTimeOffset.UtcNow }, null)).StatusCode);
            }
        }

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PostAsJsonAsync("/v1/admin/register", new { username = "x", password = "y" }, Ct)).StatusCode);
    }

    [Fact]
    public async Task The_owner_signs_in_with_the_password_and_an_authenticator_code()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase());
        var owner = await OwnerAsync(api);
        var client = Browser(api);

        var session = await SignInAsync(client, owner);

        Assert.Equal(("wlasciciel", 10), (session.GetProperty("username").GetString(), session.GetProperty("recoveryCodesLeft").GetInt32()));
        var read = await client.GetAsync("/v1/admin/session", Ct);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var cookie = Assert.Single(read.RequestMessage!.Headers.GetValues("Cookie"));
        Assert.Contains("pss.owner=", cookie, StringComparison.Ordinal);

        var logout = await PostAsync(client, "/v1/admin/logout", new { }, session.GetProperty("csrfToken").GetString());
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/v1/admin/session", Ct)).StatusCode);
    }

    [Fact]
    public async Task A_recovery_code_signs_in_once()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase());
        var owner = await OwnerAsync(api);
        async Task<HttpStatusCode> UseAsync(string code)
        {
            var client = Browser(api);
            await client.PostAsJsonAsync("/v1/admin/login", new { username = owner.Username, password = owner.Password }, Ct);
            return (await client.PostAsJsonAsync("/v1/admin/login/code", new { recoveryCode = code }, Ct)).StatusCode;
        }

        var code = owner.RecoveryCodes[0];
        Assert.Equal(HttpStatusCode.OK, await UseAsync(code));
        Assert.Equal(HttpStatusCode.Unauthorized, await UseAsync(code));

        await using var db = new GameDbContext(SqlServerFixture.Options(api.ConnectionString));
        var stored = await db.UserTokens.SingleAsync(t => t.Name == "RecoveryCodes", Ct);
        Assert.DoesNotContain(code, stored.Value!, StringComparison.Ordinal);
        Assert.Equal(9, stored.Value!.Split(';').Length);
        Assert.Contains(OwnerUserStore.Hash(owner.RecoveryCodes[1]), stored.Value, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, await UseAsync(owner.RecoveryCodes[1].ToLowerInvariant()));
    }

    [Fact]
    public async Task Wrong_credentials_fail_alike_and_five_failures_lock_the_account()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase());
        var owner = await OwnerAsync(api);
        var client = Browser(api);

        var unknown = await client.PostAsJsonAsync("/v1/admin/login", new { username = "ktos", password = owner.Password }, Ct);
        Assert.Equal((HttpStatusCode.Unauthorized, "owner-login-failed"), (unknown.StatusCode, await unknown.CodeAsync(Ct)));
        HttpResponseMessage last = unknown;
        for (var i = 0; i < 5; i++)
        {
            last = await client.PostAsJsonAsync("/v1/admin/login", new { username = owner.Username, password = "zle-haslo-zle-haslo" }, Ct);
        }

        Assert.Equal((HttpStatusCode.Locked, "owner-locked-out"), (last.StatusCode, await last.CodeAsync(Ct)));
        var right = await client.PostAsJsonAsync("/v1/admin/login", new { username = owner.Username, password = owner.Password }, Ct);
        Assert.Equal(HttpStatusCode.Locked, right.StatusCode);
    }

    [Fact]
    public async Task A_signed_in_post_needs_its_anti_forgery_token()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase());
        var client = Browser(api);
        var session = await SignInAsync(client, await OwnerAsync(api));
        var path = $"/v1/admin/leaderboard/{Guid.NewGuid()}/hide";

        var missing = await PostAsync(client, path, new { reason = "x" }, null);
        var wrong = await PostAsync(client, path, new { reason = "x" }, "not-a-token");
        var right = await PostAsync(client, path, new { reason = "x" }, session.GetProperty("csrfToken").GetString());

        Assert.Equal((HttpStatusCode.BadRequest, "antiforgery-failed"), (missing.StatusCode, await missing.CodeAsync(Ct)));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal((HttpStatusCode.NotFound, "entry-not-found"), (right.StatusCode, await right.CodeAsync(Ct)));
    }

    [Fact]
    public async Task The_owner_hides_and_restores_a_published_entry()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase());
        var player = api.CreateClient();
        var (id, secret, career) = await player.CreateCareerAsync(Ct, ranked: true);
        await player.ActAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), type = "retire" }, Ct);
        await player.PostLeaderboardEntryAsync(id, secret, new { nickname = "ZLY" }, Ct);
        var client = Browser(api);
        var csrf = (await SignInAsync(client, await OwnerAsync(api))).GetProperty("csrfToken").GetString();

        var listed = await client.GetFromJsonAsync<JsonElement>("/v1/admin/leaderboard", Ct);
        var entryId = listed.GetProperty("entries")[0].GetProperty("entryId").GetGuid();
        var hidden = await PostAsync(client, $"/v1/admin/leaderboard/{entryId}/hide", new { reason = "Obraźliwy nick" }, csrf);

        Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        var publicTable = await player.GetFromJsonAsync<JsonElement>("/v1/leaderboard", Ct);
        Assert.Equal(0, publicTable.GetProperty("entries").GetArrayLength());

        var restored = await PostAsync(client, $"/v1/admin/leaderboard/{entryId}/restore", new { reason = "Pomyłka" }, csrf);
        Assert.Equal(1, (await restored.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("rank").GetInt32());
        var audit = await client.GetFromJsonAsync<JsonElement>("/v1/admin/audit", Ct);
        Assert.Equal(
            ["leaderboard.restore", "leaderboard.hide"],
            audit.EnumerateArray().Select(a => a.GetProperty("action").GetString()));
        Assert.Equal("wlasciciel", audit[0].GetProperty("actor").GetString());
    }

    [Fact]
    public async Task The_command_creates_one_owner_and_a_reset_replaces_the_credentials()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase());
        var first = await OwnerAsync(api);
        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var second = await OwnerCreateCommand.CreateAsync(users, "drugi");
        var reset = (await OwnerCreateCommand.ResetAsync(users)).Credentials!;

        Assert.NotNull(second.Error);
        Assert.Equal(first.Username, reset.Username);
        Assert.NotEqual(first.Password, reset.Password);
        Assert.NotEqual(first.AuthenticatorKey, reset.AuthenticatorKey);
        Assert.Equal(10, reset.RecoveryCodes.Count);
        Assert.StartsWith("otpauth://totp/", reset.AuthenticatorUri, StringComparison.Ordinal);
        var stale = Browser(api);
        var old = await stale.PostAsJsonAsync("/v1/admin/login", new { username = first.Username, password = first.Password }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        await SignInAsync(Browser(api), reset);
    }
}
