using System.Net;
using PolskiStreamerSimulator.Tests.Support;
using PolskiStreamerSimulator.WebApp;

namespace PolskiStreamerSimulator.Tests.WebApp;

/// <summary>M5-D (#38): every API response, an error included, carries the security headers; bodies have a default size limit.</summary>
public sealed class SecurityHeadersTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = api.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/v1/career-options", HttpStatusCode.OK)]
    [InlineData("/v1/careers/0199e3a0-0000-7000-8000-000000000001", HttpStatusCode.Unauthorized)]
    [InlineData("/v1/no-such-route", HttpStatusCode.NotFound)]
    public async Task A_response_forbids_sniffing_framing_referrers_and_storing(string path, HttpStatusCode status)
    {
        var response = await client.GetAsync(path, Ct);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Equal("same-origin", Header(response, "Cross-Origin-Resource-Policy"));
        Assert.Equal(SecurityHeaders.ApiContentSecurityPolicy, Header(response, "Content-Security-Policy"));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task The_public_leaderboard_keeps_its_own_caching()
    {
        var response = await client.GetAsync("/v1/leaderboard", Ct);

        Assert.Equal("public, max-age=30", response.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
    }

    [Fact]
    public async Task A_body_over_the_default_limit_is_rejected_before_it_is_read()
    {
        var body = new string('x', (int)RequestSizeLimits.DefaultMaxBytes + 1);
        using var content = new StringContent($"{{\"nickname\":\"{body}\"}}", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/v1/careers", content, Ct);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
    }

    private static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
