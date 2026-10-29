using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Moderation;

namespace PolskiStreamerSimulator.Application.Leaderboard;

/// <summary>Announces a later submission deadline for every career of a closed season (spec M4-B §4).</summary>
public sealed record ExtendSeason(int Season, DateTimeOffset Until, string Reason, string Actor);

public sealed class ExtendSeasonHandler(ISeasonStore seasons, TimeProvider time)
{
    public async Task<AppResult<SeasonView>> Handle(ExtendSeason command, CancellationToken cancellationToken)
    {
        if (AuditActions.Reason(command.Reason) is not { } reason || string.IsNullOrWhiteSpace(command.Actor))
        {
            return AppResult<SeasonView>.Fail(ErrorCodes.ValidationFailed, $"a reason of 1-{AuditActions.MaxReasonLength} characters and an actor are required");
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

        var audit = new AuditEntry(
            command.Actor,
            AuditActions.Extend,
            season.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            reason,
            AuditActions.Json(new { deadline }),
            AuditActions.Json(new { deadline = command.Until }),
            time.GetUtcNow());
        await seasons.AddExtensionAsync(season.Number, command.Until, audit, cancellationToken);
        return AppResult<SeasonView>.Ok(SeasonViews.Of(season with { ExtendedUntil = command.Until }));
    }
}
