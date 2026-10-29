using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Application.Leaderboard;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>The API over in-memory stores with the M1 content: no database and no Docker.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string contentDirectory;

    // Program ends with RunJasperFxCommands; the test host must still start.
    static ApiFactory() => JasperFxEnvironment.AutoStartHost = true;

    public ApiFactory()
        : this(TestPaths.ContentM1)
    {
    }

    protected ApiFactory(string contentDirectory) => this.contentDirectory = contentDirectory;

    public InMemoryCareerStore Store { get; } = new();

    public InMemorySeasonStore Seasons { get; } = new();

    public InMemoryLeaderboardStore Entries { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Game", "Server=unused;Database=unused");
        builder.UseSetting("Database:InitializeOnStartup", "false");
        builder.UseSetting("Content:Directory", contentDirectory);
        // Test classes create far more careers per minute than a player; RateLimitApiTests lowers the limits.
        builder.UseSetting("RateLimits:CreatePermits", "100000");
        builder.UseSetting("RateLimits:RecoverPermits", "100000");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICareerStore>();
            services.AddSingleton<ICareerStore>(Store);
            services.RemoveAll<IContentStore>();
            var content = TestContent.Of(contentDirectory);
            services.AddSingleton<IContentStore>(new InMemoryContentStore(content));

            // The database initializer is off, so the season it would open at startup is opened here.
            Seasons.EnsureCurrentAsync(content.RulesetId, content.Digest, DateTimeOffset.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
            services.RemoveAll<ISeasonStore>();
            services.AddSingleton<ISeasonStore>(Seasons);
            services.RemoveAll<ILeaderboardStore>();
            services.AddSingleton<ILeaderboardStore>(Entries);
        });
    }
}

/// <summary>The API over in-memory stores with the M3 content, which new careers pin in production.</summary>
public sealed class M3ApiFactory() : ApiFactory(TestPaths.ContentM3);
