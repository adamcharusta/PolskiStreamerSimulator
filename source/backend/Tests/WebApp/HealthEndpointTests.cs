using Microsoft.Extensions.DependencyInjection;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Tests.Support;
using Wolverine;

namespace PolskiStreamerSimulator.Tests.WebApp;

public class HealthEndpointTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var res = await api.CreateClient().GetStringAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal("ok", res);
    }

    [Fact]
    public void The_host_runs_wolverine_over_the_test_stores()
    {
        Assert.NotNull(api.Services.GetRequiredService<IMessageBus>());
        Assert.IsType<InMemoryCareerStore>(api.Services.GetRequiredService<ICareerStore>());
    }
}
