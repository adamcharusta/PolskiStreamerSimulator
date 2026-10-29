using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolskiStreamerSimulator.Application.Careers;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

public sealed class RetentionOptions
{
    public const string Section = "Retention";

    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>Runs the retention sweep at startup and then every <see cref="RetentionOptions.Interval"/>.</summary>
public sealed partial class RetentionSweeper(RetentionSweep sweep, IOptions<RetentionOptions> options, TimeProvider time, ILogger<RetentionSweeper> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.Interval, time);
        do
        {
            await SweepOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One sweep; a failure is logged and retried at the next tick instead of stopping the host.</summary>
    public async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await sweep.RunAsync(cancellationToken);
            if (deleted > 0)
            {
                LogDeleted(logger, deleted);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            LogFailed(logger, error);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Retention sweep deleted {Count} expired careers.")]
    private static partial void LogDeleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Retention sweep failed; it runs again at the next interval.")]
    private static partial void LogFailed(ILogger logger, Exception error);
}
