using PolskiStreamerSimulator.Application.Careers;

namespace PolskiStreamerSimulator.Application.Leaderboard;

/// <summary>Announces a later submission deadline for every career of a closed season (spec M4-B §4).</summary>
public sealed record ExtendSeason(int Season, DateTimeOffset Until, string Reason, string Actor);

public sealed class ExtendSeasonHandler(ISeasonStore seasons, TimeProvider time)
{
    public async Task<AppResult<SeasonView>> Handle(ExtendSeason command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason) || string.IsNullOrWhiteSpace(command.Actor))
        {
            return AppResult<SeasonView>.Fail(ErrorCodes.ValidationFailed, "reason and actor are required");
        }

        var season = await seasons.FindAsync(command.Season, cancellationToken);
        if (season is null)
        {
            return AppResult<SeasonView>.Fail(ErrorCodes.SeasonNotFound);
        }

        if (season.SubmissionDeadline is not { } deadline || command.Until <= deadline)
        {
            return AppResult<SeasonView>.Fail(ErrorCodes.ValidationFailed, "only a closed season can be extended, and only to a later deadline");
        }

        await seasons.AddExtensionAsync(season.Number, command.Until, command.Reason.Trim(), command.Actor, time.GetUtcNow(), cancellationToken);
        return AppResult<SeasonView>.Ok(SeasonViews.Of(season with { ExtendedUntil = command.Until }));
    }
}
