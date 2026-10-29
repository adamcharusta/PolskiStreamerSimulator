using System.Globalization;
using JasperFx.CommandLine;
using PolskiStreamerSimulator.Application.Leaderboard;

namespace PolskiStreamerSimulator.WebApp;

public sealed class SeasonExtendInput : NetCoreInput
{
    [Description("Season number")]
    public int SeasonFlag { get; set; }

    [Description("New submission deadline, ISO 8601 with offset, e.g. 2027-05-01T23:59:59+02:00")]
    public string UntilFlag { get; set; } = "";

    [Description("Why the deadline moves; kept as the audit reason")]
    public string ReasonFlag { get; set; } = "";
}

/// <summary>
/// <c>season-extend</c>: announces a later submission deadline for every career of a closed season, for example after
/// a ranking outage (spec M4-B §4). M4-D's admin form writes the same audit record.
/// </summary>
[Description("Moves a closed season's submission deadline later for every career in it.", Name = "season-extend")]
public sealed class SeasonExtendCommand : JasperFxAsyncCommand<SeasonExtendInput>
{
    public override async Task<bool> Execute(SeasonExtendInput input)
    {
        if (!DateTimeOffset.TryParse(input.UntilFlag, CultureInfo.InvariantCulture, DateTimeStyles.None, out var until))
        {
            await Console.Error.WriteLineAsync("--until must be an ISO 8601 date with an offset.");
            return false;
        }

        using var host = input.BuildHost();
        var handler = ActivatorUtilities.CreateInstance<ExtendSeasonHandler>(host.Services);
        var result = await handler.Handle(new ExtendSeason(input.SeasonFlag, until, input.ReasonFlag, "command"), CancellationToken.None);
        if (!result.IsSuccess)
        {
            await Console.Error.WriteLineAsync($"Season not extended: {result.Error.Code} {result.Error.Detail}");
            return false;
        }

        await Console.Out.WriteLineAsync($"Season {result.Value.Number} accepts results until {result.Value.SubmissionDeadline:O}.");
        return true;
    }
}
