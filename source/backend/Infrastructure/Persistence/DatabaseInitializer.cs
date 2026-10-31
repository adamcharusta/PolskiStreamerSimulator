using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Application.Moderation;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>
/// Before the API serves requests: optionally migrates, stores the repository content and publishes it as a release
/// when its digest is new, then loads the newest publication and makes the open season match it (spec M4-B §4, M4-E §3).
/// </summary>
public sealed class DatabaseInitializer(
    IDbContextFactory<GameDbContext> contexts,
    IContentStore contents,
    DirectoryCurrentContent repository,
    IContentPublications publications,
    PublishedContentLoader loader,
    TimeProvider time,
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

        await contents.EnsureStoredAsync(repository.Files, cancellationToken);
        var now = time.GetUtcNow();
        var digest = repository.Snapshot.Digest;
        var release = new NewPublication(digest, repository.MechanicsHash, PublicationKind.Release, null, ContentAuditActions.DeploymentActor, "repository content", now);
        var audit = new AuditEntry(ContentAuditActions.DeploymentActor, ContentAuditActions.Release, digest, "repository content", null, AuditActions.Json(new { digest, mechanicsHash = repository.MechanicsHash }), now);
        await publications.EnsureReleasedAsync(release, audit, cancellationToken);
        await loader.LoadAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
