using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PolskiStreamerSimulator.Application.Content;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>Before the API serves requests: optionally migrates, then stores the content new careers pin.</summary>
public sealed class DatabaseInitializer(
    IDbContextFactory<GameDbContext> contexts,
    IContentStore contents,
    ICurrentContent current,
    IOptions<DatabaseOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.InitializeOnStartup)
        {
            return;
        }

        if (options.Value.MigrateOnStartup)
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
        }

        await contents.EnsureStoredAsync(current.Files, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
