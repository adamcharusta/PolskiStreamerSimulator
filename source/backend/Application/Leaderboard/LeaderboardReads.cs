using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Leaderboard;

// Summaries of contract records stay on one line: the OpenAPI document copies them, and line breaks differ by OS.

/// <summary>A season with its dates; the deadline is null while it is open, and extended once the owner announced a later one.</summary>
public sealed record SeasonView(int Number, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt, DateTimeOffset? SubmissionDeadline, bool Extended, bool Current);

/// <summary>A public leaderboard row: only the fields every visitor may see.</summary>
public sealed record LeaderboardRowView(int Rank, string Nickname, long Score, int WeeksPlayed, Difficulty Difficulty, EndingReason Ending, DateTimeOffset SubmittedAt);

/// <summary>A season's top entries in leaderboard order.</summary>
public sealed record LeaderboardView(SeasonView Season, IReadOnlyList<LeaderboardRowView> Entries);

/// <summary>The top of one season (the current one when <see cref="Season"/> is null); at most <see cref="MaxLimit"/> rows.</summary>
public sealed record GetLeaderboard(int? Season, int? Limit)
{
    public const int MaxLimit = 100;
}

public sealed record ListSeasons
{
    public static readonly ListSeasons Instance = new();
}

public sealed class GetLeaderboardHandler(ISeasonStore seasons, ILeaderboardStore entries)
{
    public async Task<AppResult<LeaderboardView>> Handle(GetLeaderboard query, CancellationToken cancellationToken)
    {
        var season = query.Season is { } number
            ? await seasons.FindAsync(number, cancellationToken)
            : await seasons.CurrentAsync(cancellationToken);
        if (season is null)
        {
            return AppResult<LeaderboardView>.Fail(ErrorCodes.SeasonNotFound);
        }

        var limit = Math.Clamp(query.Limit ?? GetLeaderboard.MaxLimit, 1, GetLeaderboard.MaxLimit);
        var top = await entries.TopAsync(season.Number, limit, cancellationToken);
        var rows = top.Select((e, i) => new LeaderboardRowView(i + 1, e.Nickname, e.Score, e.WeeksPlayed, e.Difficulty, e.Ending, e.SubmittedAt));
        return AppResult<LeaderboardView>.Ok(new LeaderboardView(SeasonViews.Of(season), [.. rows]));
    }
}

public sealed class ListSeasonsHandler(ISeasonStore seasons)
{
    public async Task<IReadOnlyList<SeasonView>> Handle(ListSeasons query, CancellationToken cancellationToken)
        => [.. (await seasons.ListAsync(cancellationToken)).Select(SeasonViews.Of)];
}

internal static class SeasonViews
{
    public static SeasonView Of(Season s) => new(
        s.Number,
        s.OpenedAt,
        s.ClosedAt,
        s.SubmissionDeadline,
        s.ClosedAt is { } closed && s.SubmissionDeadline > closed + Season.SubmissionWindow,
        s.ClosedAt is null);
}
