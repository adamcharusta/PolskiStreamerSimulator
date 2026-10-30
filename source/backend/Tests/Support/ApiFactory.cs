using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Application.Moderation;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>The API over in-memory stores with the M1 content: no database and no Docker.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string contentDirectory;
    private InMemorySeasonStore? seasons;
    private InMemoryLeaderboardStore? entries;

    // Program ends with RunJasperFxCommands; the test host must still start.
    static ApiFactory() => JasperFxEnvironment.AutoStartHost = true;

    public ApiFactory()
        : this(TestPaths.ContentM1)
    {
    }

    protected ApiFactory(string contentDirectory) => this.contentDirectory = contentDirectory;

    public InMemoryCareerStore Store { get; } = new();

    public InMemoryAuditTrail Audit { get; } = new();

    public InMemoryTelemetryStore Telemetry { get; } = new();

    public InMemorySeasonStore Seasons => seasons ??= new InMemorySeasonStore(Audit);

    public InMemoryLeaderboardStore Entries => entries ??= new InMemoryLeaderboardStore(Audit);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Game", "Server=unused;Database=unused");
        builder.UseSetting("Database:InitializeOnStartup", "false");
        builder.UseSetting("Content:Directory", contentDirectory);
        // Test classes create far more careers per minute than a player; RateLimitApiTests lowers the limits.
        builder.UseSetting("RateLimits:CreatePermits", "100000");
        builder.UseSetting("RateLimits:RecoverPermits", "100000");
        builder.UseSetting("RateLimits:OwnerSignInPermits", "100000");
        builder.UseSetting("RateLimits:TelemetryPermits", "100000");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICareerStore>();
            services.AddSingleton<ICareerStore>(Store);
            services.RemoveAll<IContentStore>();
            var content = TestContent.Of(contentDirectory);
            services.AddSingleton<IContentStore>(new InMemoryContentStore(content));

            // The database initializer is off, so the season it would open at startup is opened here.
            var mechanics = new TestCurrentContent(contentDirectory).MechanicsHash;
            Seasons.EnsureCurrentAsync(content.RulesetId, content.Digest, mechanics, DateTimeOffset.UtcNow, CancellationToken.None).GetAwaiter().GetResult();

            // Without the initializer nothing is published; new careers pin the repository content directly.
            services.RemoveAll<ICurrentContent>();
            services.AddSingleton<ICurrentContent>(provider => provider.GetRequiredService<DirectoryCurrentContent>());
            services.RemoveAll<ISeasonStore>();
            services.AddSingleton<ISeasonStore>(Seasons);
            services.RemoveAll<ILeaderboardStore>();
            services.AddSingleton<ILeaderboardStore>(Entries);
            services.RemoveAll<IModerationStore>();
            services.AddSingleton<IModerationStore>(Entries);
            services.RemoveAll<IAuditTrail>();
            services.AddSingleton<IAuditTrail>(Audit);
            services.AddSingleton<ISaveSlotStore>(new InMemorySaveSlotStore(Store));
            services.AddSingleton<PolskiStreamerSimulator.Application.Telemetry.ITelemetryStore>(Telemetry);
        });
    }
}

/// <summary>The API over in-memory stores with the M3 content, which new careers pin in production.</summary>
public sealed class M3ApiFactory() : ApiFactory(TestPaths.ContentM3);
