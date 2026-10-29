using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>The API on a real SQL Server database; startup migrates it and stores the current content.</summary>
public sealed class SqlApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    static SqlApiFactory() => JasperFxEnvironment.AutoStartHost = true;

    public string ConnectionString => connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Game", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Content:Directory", TestPaths.ContentM1);
        // Test classes create far more careers per minute than a player; RateLimitApiTests lowers the limits.
        builder.UseSetting("RateLimits:CreatePermits", "100000");
        builder.UseSetting("RateLimits:RecoverPermits", "100000");
        builder.UseSetting("RateLimits:OwnerSignInPermits", "100000");
    }
}
