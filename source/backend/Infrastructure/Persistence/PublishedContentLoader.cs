using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Application.Leaderboard;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

public sealed class ContentRefreshOptions
{
    public const string Section = "ContentRefresh";

    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>Switches <see cref="PublishedContent"/> to the newest publication and aligns the open season with it.</summary>
public sealed class PublishedContentLoader(
    IContentPublications publications,
    IContentStore contents,
    PublishedContent current,
    ISeasonStore seasons,
    TimeProvider time)
{
    /// <returns>Whether the content new careers pin changed.</returns>
    public async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var latest = await publications.LatestAsync(cancellationToken)
            ?? throw new InvalidOperationException("No content publication exists; startup publishes the repository content first.");
        if (current.Number == latest.Number)
        {
            return false;
        }

        var files = await contents.FindFilesAsync(latest.Digest, cancellationToken)
            ?? throw new InvalidOperationException($"Published content {latest.Digest} is not stored.");
        var snapshot = await contents.FindAsync(latest.Digest, cancellationToken)
            ?? throw new InvalidOperationException($"Published content {latest.Digest} is not stored.");
        current.Set(latest.Number, files, snapshot);
        await seasons.EnsureCurrentAsync(snapshot.RulesetId, snapshot.Digest, latest.MechanicsHash, time.GetUtcNow(), cancellationToken);
        return true;
    }
}

/// <summary>Picks up publications made on other API instances every <see cref="ContentRefreshOptions.Interval"/> (spec M4-E §3).</summary>
public sealed partial class PublishedContentRefresher(
    PublishedContentLoader loader,
    PublishedContent current,
    IOptions<ContentRefreshOptions> options,
    IOptions<DatabaseOptions> database,
    TimeProvider time,
    ILogger<PublishedContentRefresher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || !database.Value.InitializeOnStartup)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (await loader.LoadAsync(stoppingToken))
                {
                    LogSwitched(logger, current.Number ?? 0);
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                LogFailed(logger, error);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "New careers now pin content publication {Number}.")]
    private static partial void LogSwitched(ILogger logger, int number);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the newest content publication failed; retrying at the next tick.")]
    private static partial void LogFailed(ILogger logger, Exception error);
}
