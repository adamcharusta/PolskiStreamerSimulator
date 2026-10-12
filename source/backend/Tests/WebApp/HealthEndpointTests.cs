using Microsoft.AspNetCore.Mvc.Testing;

namespace PolskiStreamerSimulator.Tests.WebApp;

public class HealthEndpointTests
{
    [Fact]
    public async Task Health_returns_ok()
    {
        await using var app = new WebApplicationFactory<Program>();
        var res = await app.CreateClient().GetStringAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal("ok", res);
    }
}
