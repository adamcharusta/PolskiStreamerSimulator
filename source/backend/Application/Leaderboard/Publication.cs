using System.Text;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;

namespace PolskiStreamerSimulator.Application.Leaderboard;

/// <summary>What the ending screen can offer for a career's result.</summary>
public enum EntryStatus
{
    Unranked,
    NotEnded,

    /// <summary>The career can publish now; a closed season also reports its deadline.</summary>
    Open,

    /// <summary>The season's submission deadline has passed.</summary>
    Closed,
    Published,
}

// Summaries of contract records stay on one line: the OpenAPI document copies them, and line breaks differ by OS.

/// <summary>Whether and until when a career can publish its result, and the published entry with its season rank.</summary>
public sealed record EntryStatusView(EntryStatus Status, int? Season, DateTimeOffset? SubmissionDeadline, PublishedEntryView? Entry);

/// <summary>A career's own published entry; the rank is null while the owner hides the entry.</summary>
public sealed record PublishedEntryView(string Nickname, long Score, int WeeksPlayed, DateTimeOffset SubmittedAt, int? Rank);

public sealed record GetEntryStatus(Guid CareerId, string? Secret)
{
    // Wolverine logs a failed message with ToString(); the bearer secret must never reach a log.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***");
        return true;
    }
}

/// <summary>Publishes the career's server-computed result; the client sends only the nickname (spec M4-B §6).</summary>
public sealed record PublishResult(Guid CareerId, string? Secret, string? Nickname)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***, Nickname = {Nickname}");
        return true;
    }
}

/// <summary>The publication status of one career, shared by the status read and the publication.</summary>
public sealed class LeaderboardPublication(ISeasonStore seasons, ILeaderboardStore entries, TimeProvider time)
{
    public async Task<EntryStatusView> StatusAsync(StoredCareer career, CancellationToken cancellationToken)
    {
        if (await entries.FindByCareerAsync(career.Id, cancellationToken) is { } entry)
        {
            return await PublishedAsync(entry, cancellationToken);
        }

        if (career.Season is not { } number || await seasons.FindAsync(number, cancellationToken) is not { } season)
        {
            return new EntryStatusView(EntryStatus.Unranked, null, null, null);
        }

        if (career.State.Status != CareerStatus.Ended || career.State.FinalScore is null)
        {
            return new EntryStatusView(EntryStatus.NotEnded, season.Number, season.SubmissionDeadline, null);
        }

        var status = season.AcceptsAt(time.GetUtcNow()) ? EntryStatus.Open : EntryStatus.Closed;
        return new EntryStatusView(status, season.Number, season.SubmissionDeadline, null);
    }

    public async Task<EntryStatusView> PublishedAsync(LeaderboardEntry entry, CancellationToken cancellationToken)
    {
        var season = await seasons.FindAsync(entry.Season, cancellationToken);
        var rank = entry.HiddenAt is null ? await entries.RankAsync(entry, cancellationToken) : (int?)null;
        return new EntryStatusView(
            EntryStatus.Published,
            entry.Season,
            season?.SubmissionDeadline,
            new PublishedEntryView(entry.Nickname, entry.Score, entry.WeeksPlayed, entry.SubmittedAt, rank));
    }
}

public sealed class GetEntryStatusHandler(CareerAccess access, LeaderboardPublication publication)
{
    public async Task<AppResult<EntryStatusView>> Handle(GetEntryStatus query, CancellationToken cancellationToken)
    {
        var opened = await access.OpenAsync(query.CareerId, query.Secret, cancellationToken);
        return opened.IsSuccess
            ? AppResult<EntryStatusView>.Ok(await publication.StatusAsync(opened.Value, cancellationToken))
            : AppResult<EntryStatusView>.Fail(opened.Error);
    }
}

public sealed class PublishResultHandler(CareerAccess access, LeaderboardPublication publication, ILeaderboardStore entries, TimeProvider time)
{
    public async Task<AppResult<EntryStatusView>> Handle(PublishResult command, CancellationToken cancellationToken)
    {
        var opened = await access.OpenAsync(command.CareerId, command.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<EntryStatusView>.Fail(opened.Error);
        }

        var career = opened.Value;
        var status = await publication.StatusAsync(career, cancellationToken);
        switch (status.Status)
        {
            case EntryStatus.Published:
                // The first publication is final; a repeat, with any nickname, returns it.
                return AppResult<EntryStatusView>.Ok(status);
            case EntryStatus.Unranked:
                return AppResult<EntryStatusView>.Fail(ErrorCodes.CareerUnranked);
            case EntryStatus.NotEnded:
                return AppResult<EntryStatusView>.Fail(ErrorCodes.CareerNotEnded);
            case EntryStatus.Closed:
                return AppResult<EntryStatusView>.Fail(ErrorCodes.SubmissionClosed);
        }

        if (!LeaderboardNicknames.IsValid(command.Nickname))
        {
            return AppResult<EntryStatusView>.Fail(ErrorCodes.ValidationFailed, LeaderboardNicknames.Rule);
        }

        var state = career.State;
        var now = time.GetUtcNow();
        var entry = new LeaderboardEntry(
            Guid.CreateVersion7(now),
            status.Season!.Value,
            career.Id,
            LeaderboardNicknames.Normalize(command.Nickname!),
            state.FinalScore!.FinalScore,
            state.LastSettledWeek,
            state.Difficulty,
            state.Ending!.Value,
            state.RulesetId,
            state.StateVersion,
            StateHash.Of(state),
            now);
        if (!await entries.AddAsync(entry, cancellationToken))
        {
            // A concurrent publication of the same career stored its entry first.
            entry = await entries.FindByCareerAsync(career.Id, cancellationToken)
                ?? throw new InvalidOperationException("The entry of a duplicate publication is missing.");
        }

        return AppResult<EntryStatusView>.Ok(await publication.PublishedAsync(entry, cancellationToken));
    }
}
