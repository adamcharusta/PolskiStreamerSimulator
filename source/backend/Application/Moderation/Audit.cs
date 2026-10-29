using System.Text.Json;
using PolskiStreamerSimulator.Application.Careers;

namespace PolskiStreamerSimulator.Application.Moderation;

/// <summary>
/// One owner change to keep: who, what, on which target, why and when, with the values before and after as JSON. A
/// store writes it in the transaction of the change it records (spec M4-D §5).
/// </summary>
public sealed record AuditEntry(string Actor, string Action, string Target, string Reason, string? OldJson, string? NewJson, DateTimeOffset At);

/// <summary>A stored audit record.</summary>
public sealed record AuditRecord(long Id, AuditEntry Entry);

// Summaries of contract records stay on one line: the OpenAPI document copies them, and line breaks differ by OS.

/// <summary>An audit record: who changed what and why, with the values before and after as JSON text.</summary>
public sealed record AuditRecordView(long Id, DateTimeOffset At, string Actor, string Action, string Target, string Reason, string? OldJson, string? NewJson);

public static class AuditActions
{
    public const string Hide = "leaderboard.hide";
    public const string Restore = "leaderboard.restore";
    public const string Extend = "season.extend";

    /// <summary>The actor of a change made with a server command rather than the owner's sign-in.</summary>
    public const string CommandActor = "command";

    /// <summary>The longest reason the owner may give, after trimming.</summary>
    public const int MaxReasonLength = 500;

    /// <summary>The reason trimmed, or <c>null</c> when it is missing or longer than <see cref="MaxReasonLength"/>.</summary>
    public static string? Reason(string? reason)
        => reason?.Trim() is { Length: > 0 and <= MaxReasonLength } trimmed ? trimmed : null;

    /// <summary>Compact camelCase JSON of a value recorded before or after a change.</summary>
    public static string Json(object value) => JsonSerializer.Serialize(value, ContractJson.Options);
}

/// <summary>Port for reading the audit trail; each change's own store writes its records.</summary>
public interface IAuditTrail
{
    /// <summary>The newest <paramref name="limit"/> records, newest first.</summary>
    Task<IReadOnlyList<AuditRecord>> RecentAsync(int limit, CancellationToken cancellationToken);
}

/// <summary>The newest audit records (at most <see cref="MaxLimit"/>, <see cref="DefaultLimit"/> by default).</summary>
public sealed record ListAudit(int? Limit)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
}

public sealed class ListAuditHandler(IAuditTrail audit)
{
    public async Task<IReadOnlyList<AuditRecordView>> Handle(ListAudit query, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(query.Limit ?? ListAudit.DefaultLimit, 1, ListAudit.MaxLimit);
        return [.. (await audit.RecentAsync(limit, cancellationToken)).Select(r => new AuditRecordView(
            r.Id, r.Entry.At, r.Entry.Actor, r.Entry.Action, r.Entry.Target, r.Entry.Reason, r.Entry.OldJson, r.Entry.NewJson))];
    }
}
