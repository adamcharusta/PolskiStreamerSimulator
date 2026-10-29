using JasperFx.CommandLine;
using PolskiStreamerSimulator.Application.Careers;

namespace PolskiStreamerSimulator.WebApp;

/// <summary>
/// <c>retention-sweep</c>: deletes expired careers once and exits. Run it after restoring a backup, before the API
/// serves requests again, so an older backup cannot revive expired careers (technical architecture §4).
/// </summary>
[Description("Deletes expired careers once and exits; run after a restore, before the API reopens.", Name = "retention-sweep")]
public sealed class RetentionSweepCommand : JasperFxAsyncCommand<NetCoreInput>
{
    public override async Task<bool> Execute(NetCoreInput input)
    {
        using var host = input.BuildHost();
        var deleted = await host.Services.GetRequiredService<RetentionSweep>().RunAsync(CancellationToken.None);
        await Console.Out.WriteLineAsync($"Retention sweep deleted {deleted} expired careers.");
        return true;
    }
}
