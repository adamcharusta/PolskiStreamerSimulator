using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Moderation;

/// <summary>What setting an entry hidden or visible did.</summary>
public enum ModerationOutcome
{
    Changed,
    NotFound,

    /// <summary>The entry was already hidden (for a hide) or visible (for a restore); nothing was written.</summary>
    Unchanged,
}

/// <summary>Port for owner moderation of leaderboard entries (spec M4-D §5).</summary>
public interface IModerationStore
{
    /// <summary>Every entry of the season, hidden ones included, in leaderboard order.</summary>
    Task<IReadOnlyList<LeaderboardEntry>> ListAsync(int season, CancellationToken cancellationToken);

    /// <summary>
    /// Hides the entry with <paramref name="audit"/>'s reason, or restores it when <paramref name="hidden"/> is false,
    /// and stores the audit record in the same transaction; the score and order fields never change.
    /// </summary>
    Task<(ModerationOutcome Outcome, LeaderboardEntry? Entry)> SetHiddenAsync(Guid entryId, bool hidden, AuditEntry audit, CancellationToken cancellationToken);
}

// Summaries of contract records stay on one line: the OpenAPI document copies them, and line breaks differ by OS.

/// <summary>An entry as the owner sees it: the public fields, its receipt, whether and why it is hidden, and its public rank while visible.</summary>
public sealed record ModeratedEntryView(
    Guid EntryId,
    Guid CareerId,
    int? Rank,
    string Nickname,
    long Score,
    int WeeksPlayed,
    Difficulty Difficulty,
    EndingReason Ending,
    DateTimeOffset SubmittedAt,
    string RulesetId,
    long FinalStateVersion,
    string FinalStateHash,
    DateTimeOffset? HiddenAt,
    string? HiddenReason);

/// <summary>A season with every one of its entries.</summary>
public sealed record ModeratedSeasonView(SeasonView Season, IReadOnlyList<ModeratedEntryView> Entries);

/// <summary>Every entry of a season (the current one when <see cref="Season"/> is null), for the owner.</summary>
public sealed record ListModeratedEntries(int? Season);

/// <summary>Hides (or revokes) an entry, or restores it when <see cref="Hide"/> is false, by <see cref="Actor"/> for <see cref="Reason"/>.</summary>
public sealed record ModerateEntry(Guid EntryId, bool Hide, string? Reason, string Actor);

public sealed class ListModeratedEntriesHandler(ISeasonStore seasons, IModerationStore moderation)
{
    public async Task<AppResult<ModeratedSeasonView>> Handle(ListModeratedEntries query, CancellationToken cancellationToken)
    {
        var season = query.Season is { } number
            ? await seasons.FindAsync(number, cancellationToken)
            : await seasons.CurrentAsync(cancellationToken);
        if (season is null)
        {
            return AppResult<ModeratedSeasonView>.Fail(ErrorCodes.SeasonNotFound);
        }

        var entries = await moderation.ListAsync(season.Number, cancellationToken);
        var rank = 0;
        var views = entries.Select(e => View(e, e.HiddenAt is null ? ++rank : null));
        return AppResult<ModeratedSeasonView>.Ok(new ModeratedSeasonView(SeasonViews.Of(season), [.. views]));
    }

    internal static ModeratedEntryView View(LeaderboardEntry e, int? rank) => new(
        e.Id, e.CareerId, rank, e.Nickname, e.Score, e.WeeksPlayed, e.Difficulty, e.Ending, e.SubmittedAt, e.RulesetId, e.FinalStateVersion, e.FinalStateHash, e.HiddenAt, e.HiddenReason);
}

public sealed class ModerateEntryHandler(IModerationStore moderation, ILeaderboardStore entries, TimeProvider time)
{
    public async Task<AppResult<ModeratedEntryView>> Handle(ModerateEntry command, CancellationToken cancellationToken)
    {
        if (AuditActions.Reason(command.Reason) is not { } reason)
        {
            return AppResult<ModeratedEntryView>.Fail(ErrorCodes.ValidationFailed, $"reason must have 1-{AuditActions.MaxReasonLength} characters");
        }

        var audit = new AuditEntry(
            command.Actor,
            command.Hide ? AuditActions.Hide : AuditActions.Restore,
            command.EntryId.ToString("D"),
            reason,
            null,
            null,
            time.GetUtcNow());
        var (outcome, entry) = await moderation.SetHiddenAsync(command.EntryId, command.Hide, audit, cancellationToken);
        return outcome switch
        {
            ModerationOutcome.NotFound => AppResult<ModeratedEntryView>.Fail(ErrorCodes.EntryNotFound),
            ModerationOutcome.Unchanged => AppResult<ModeratedEntryView>.Fail(command.Hide ? ErrorCodes.EntryHidden : ErrorCodes.EntryVisible),
            _ => AppResult<ModeratedEntryView>.Ok(ListModeratedEntriesHandler.View(
                entry!,
                entry!.HiddenAt is null ? await entries.RankAsync(entry, cancellationToken) : null)),
        };
    }
}

/// <summary>The values a hide or restore records before and after the change.</summary>
public static class HiddenState
{
    public static string Json(DateTimeOffset? hiddenAt, string? hiddenReason) => AuditActions.Json(new { hiddenAt, hiddenReason });
}
