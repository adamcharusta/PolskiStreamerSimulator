using System.Globalization;
using System.Text;
using System.Text.Json;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Application.Moderation;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Content;

// Summaries of contract records stay on one line: the OpenAPI document copies them, and line breaks differ by OS.

/// <summary>A publication: its number, digest, mechanics fingerprint, kind, the publication it copies, who published it, why and when.</summary>
public sealed record ContentPublicationView(int Number, string Digest, string MechanicsHash, PublicationKind Kind, int? SourceNumber, string Actor, string Reason, DateTimeOffset PublishedAt);

/// <summary>An event of the draft: its ID, version, phase and title for the list, and the whole event as JSON.</summary>
public sealed record DraftEventView(string Id, int? Version, string? Phase, string? Title, JsonElement Event);

/// <summary>The draft with its check: problems that block publication and the diff against the newest publication.</summary>
public sealed record ContentDraftView(
    int BaseNumber,
    int Revision,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    bool BehindCurrent,
    JsonElement Balance,
    IReadOnlyList<DraftEventView> Events,
    IReadOnlyList<string> Problems,
    ContentDiffView Diff);

/// <summary>The newest publication and the owner's draft, if any.</summary>
public sealed record ContentAdminView(ContentPublicationView Current, ContentDraftView? Draft);

/// <summary>Both files of a publication as their exact stored text, so their SHA-256 reproduces the digest.</summary>
public sealed record ContentExportView(int Number, string Digest, string MechanicsHash, string BalanceJson, string EventsJson);

public sealed record GetContentAdmin
{
    public static readonly GetContentAdmin Instance = new();
}

public sealed record CreateContentDraft(int? FromPublication, string Actor);

public sealed record DiscardContentDraft(int Revision, string Actor);

public sealed record PutDraftEvent(string Id, int Revision, JsonElement Event, string Actor);

public sealed record RemoveDraftEvent(string Id, int Revision, string Actor);

public sealed record PutDraftBalance(int Revision, JsonElement Balance, string Actor);

public sealed record SimulateContentDraft
{
    public static readonly SimulateContentDraft Instance = new();
}

public sealed record PublishContentDraft(int Revision, string? Reason, string Actor);

public sealed record RevertContentPublication(int Number, string? Reason, string Actor);

public sealed record ListContentPublications
{
    public static readonly ListContentPublications Instance = new();
}

public sealed record ExportContentPublication(int Number);

public static class ContentAuditActions
{
    public const string DraftCreate = "content.draft.create";
    public const string DraftDiscard = "content.draft.discard";
    public const string DraftEvent = "content.draft.event";
    public const string DraftEventRemove = "content.draft.event-remove";
    public const string DraftBalance = "content.draft.balance";
    public const string Publish = "content.publish";
    public const string Revert = "content.revert";
    public const string Release = "content.release";

    /// <summary>The actor of a release a deployment shipped.</summary>
    public const string DeploymentActor = "deployment";
}

/// <summary>The draft workflow shared by the content handlers (spec M4-E §5–§6).</summary>
public sealed class ContentWorkspace(
    IContentPublications publications,
    IContentStore contents,
    IContentCompiler compiler,
    WeekEngine engine,
    ContentSimulation simulation,
    PublishedContent current,
    ISeasonStore seasons,
    TimeProvider time)
{
    public async Task<ContentPublication> LatestAsync(CancellationToken cancellationToken)
        => await publications.LatestAsync(cancellationToken) ?? throw new InvalidOperationException("No content publication exists yet.");

    public async Task<ContentFiles> FilesAsync(string digest, CancellationToken cancellationToken)
        => await contents.FindFilesAsync(digest, cancellationToken) ?? throw new InvalidOperationException($"Content snapshot {digest} is not stored.");

    /// <summary>Every problem that blocks publishing <paramref name="files"/>, and the snapshot when it parses.</summary>
    public async Task<(ContentSnapshot? Snapshot, IReadOnlyList<string> Problems)> CheckAsync(ContentFiles files, CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        ContentSnapshot? snapshot;
        try
        {
            (snapshot, var found) = compiler.Compile(files);
            problems.AddRange(found);
            if (snapshot is not null && !engine.Supports(snapshot.RulesetId))
            {
                problems.Add($"ruleset-not-registered:{snapshot.RulesetId}");
            }

            problems.AddRange(await contents.VersionConflictsAsync(files.Events, cancellationToken));
        }
        catch (JsonException error)
        {
            snapshot = null;
            problems.Add($"json:{error.Message}");
        }

        return (problems.Count == 0 ? snapshot : null, problems);
    }

    public async Task<ContentDraftView> ViewAsync(ContentDraft draft, CancellationToken cancellationToken)
    {
        var latest = await LatestAsync(cancellationToken);
        var (_, problems) = await CheckAsync(draft.Files, cancellationToken);
        ContentDiffView diff;
        try
        {
            diff = ContentDiff.Between(await FilesAsync(latest.Digest, cancellationToken), draft.Files);
        }
        catch (JsonException)
        {
            diff = new ContentDiffView([], [], true, false);
        }

        using var balance = JsonDocument.Parse(draft.BalanceJson);
        using var events = JsonDocument.Parse(draft.EventsJson);
        var list = events.RootElement.TryGetProperty("events", out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).Select(EventView).ToList()
            : [];
        return new ContentDraftView(
            draft.BaseNumber,
            draft.Revision,
            draft.UpdatedAt,
            draft.UpdatedBy,
            draft.BaseNumber != latest.Number,
            balance.RootElement.Clone(),
            list,
            problems,
            diff);
    }

    /// <summary>Stores and pins <paramref name="files"/> as a new publication, then aligns the open season with its mechanics.</summary>
    public async Task<AppResult<ContentPublicationView>> PublishAsync(
        ContentFiles files, PublicationKind kind, int? sourceNumber, int? draftRevision, string reason, string actor, CancellationToken cancellationToken)
    {
        var latest = await LatestAsync(cancellationToken);
        var latestFiles = await FilesAsync(latest.Digest, cancellationToken);
        var (snapshot, problems) = await CheckAsync(files, cancellationToken);
        if (snapshot is null)
        {
            return AppResult<ContentPublicationView>.Fail(ErrorCodes.ContentInvalid, string.Join(", ", problems));
        }

        var diff = ContentDiff.Between(latestFiles, files);
        if (diff.Unchanged || snapshot.Digest == latest.Digest)
        {
            return AppResult<ContentPublicationView>.Fail(ErrorCodes.ContentUnchanged);
        }

        var run = simulation.Run(snapshot);
        if (run.Violations.Count > 0 || run.Errors.Count > 0)
        {
            return AppResult<ContentPublicationView>.Fail(ErrorCodes.SimulationFailed, string.Join(", ", run.Violations.Concat(run.Errors).Take(5)));
        }

        await contents.EnsureStoredAsync(files, cancellationToken);
        var mechanics = ContentMechanics.Hash(files);
        var now = time.GetUtcNow();
        var audit = new AuditEntry(
            actor,
            kind == PublicationKind.Revert ? ContentAuditActions.Revert : ContentAuditActions.Publish,
            snapshot.Digest,
            reason,
            AuditActions.Json(new { number = latest.Number, digest = latest.Digest }),
            AuditActions.Json(new
            {
                digest = snapshot.Digest,
                mechanicsHash = mechanics,
                diff.Mechanical,
                items = diff.Items.Select(i => $"{i.Section}:{i.Id}:{i.Change}"),
                values = diff.Values.Select(v => v.Path),
            }),
            now);
        var stored = await publications.PublishAsync(
            new NewPublication(snapshot.Digest, mechanics, kind, sourceNumber, actor, reason, now), audit, draftRevision, cancellationToken);
        if (stored is null)
        {
            return AppResult<ContentPublicationView>.Fail(ErrorCodes.DraftStale);
        }

        current.Set(stored.Number, files, snapshot);
        await seasons.EnsureCurrentAsync(snapshot.RulesetId, snapshot.Digest, mechanics, now, cancellationToken);
        return AppResult<ContentPublicationView>.Ok(View(stored));
    }

    public static ContentPublicationView View(ContentPublication p)
        => new(p.Number, p.Digest, p.MechanicsHash, p.Kind, p.SourceNumber, p.Actor, p.Reason, p.PublishedAt);

    public AuditEntry Audit(string actor, string action, string target, string? oldJson, string? newJson)
        => new(actor, action, target, "draft edit", oldJson, newJson, time.GetUtcNow());

    public DateTimeOffset Now => time.GetUtcNow();

    private static DraftEventView EventView(JsonElement e)
    {
        string? Text(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var version = e.TryGetProperty("version", out var raw) && raw.ValueKind == JsonValueKind.Number && raw.TryGetInt32(out var n) ? n : (int?)null;
        return new DraftEventView(Text("id") ?? "", version, Text("phase"), Text("title"), e.Clone());
    }
}

public sealed class GetContentAdminHandler(ContentWorkspace workspace, IContentDrafts drafts)
{
    public async Task<ContentAdminView> Handle(GetContentAdmin query, CancellationToken cancellationToken)
    {
        var latest = await workspace.LatestAsync(cancellationToken);
        var draft = await drafts.FindAsync(cancellationToken);
        return new ContentAdminView(ContentWorkspace.View(latest), draft is null ? null : await workspace.ViewAsync(draft, cancellationToken));
    }
}

public sealed class CreateContentDraftHandler(ContentWorkspace workspace, IContentDrafts drafts, IContentPublications publications)
{
    public async Task<AppResult<ContentDraftView>> Handle(CreateContentDraft command, CancellationToken cancellationToken)
    {
        var source = command.FromPublication is { } number
            ? await publications.FindAsync(number, cancellationToken)
            : await workspace.LatestAsync(cancellationToken);
        if (source is null)
        {
            return AppResult<ContentDraftView>.Fail(ErrorCodes.PublicationNotFound);
        }

        var files = await workspace.FilesAsync(source.Digest, cancellationToken);
        var draft = new ContentDraft(Encoding.UTF8.GetString(files.Balance), Encoding.UTF8.GetString(files.Events), source.Number, 1, workspace.Now, command.Actor);
        var audit = workspace.Audit(command.Actor, ContentAuditActions.DraftCreate, source.Number.ToString(CultureInfo.InvariantCulture), null, AuditActions.Json(new { source.Digest }));
        return await drafts.CreateAsync(draft, audit, cancellationToken)
            ? AppResult<ContentDraftView>.Ok(await workspace.ViewAsync(draft, cancellationToken))
            : AppResult<ContentDraftView>.Fail(ErrorCodes.DraftExists);
    }
}

public sealed class DiscardContentDraftHandler(ContentWorkspace workspace, IContentDrafts drafts)
{
    public async Task<AppResult<bool>> Handle(DiscardContentDraft command, CancellationToken cancellationToken)
    {
        var draft = await drafts.FindAsync(cancellationToken);
        if (draft is null)
        {
            return AppResult<bool>.Fail(ErrorCodes.DraftNotFound);
        }

        var audit = workspace.Audit(command.Actor, ContentAuditActions.DraftDiscard, "draft", AuditActions.Json(new { draft.Revision, draft.BaseNumber }), null);
        return await drafts.DeleteAsync(command.Revision, audit, cancellationToken)
            ? AppResult<bool>.Ok(true)
            : AppResult<bool>.Fail(ErrorCodes.DraftStale);
    }
}

public sealed class PutDraftEventHandler(ContentWorkspace workspace, IContentDrafts drafts)
{
    public async Task<AppResult<ContentDraftView>> Handle(PutDraftEvent command, CancellationToken cancellationToken)
    {
        if (command.Event.ValueKind != JsonValueKind.Object
            || !command.Event.TryGetProperty("id", out var id)
            || id.ValueKind != JsonValueKind.String
            || id.GetString() != command.Id)
        {
            return AppResult<ContentDraftView>.Fail(ErrorCodes.ValidationFailed, "the event must be an object whose id equals the path");
        }

        return await DraftEdits.SaveAsync(workspace, drafts, command.Revision, command.Actor, ContentAuditActions.DraftEvent, command.Id, draft =>
        {
            var before = DraftDocuments.Event(draft.EventsJson, command.Id);
            return (draft.BalanceJson, DraftDocuments.PutEvent(draft.EventsJson, command.Id, command.Event), before, command.Event.GetRawText());
        }, cancellationToken);
    }
}

public sealed class RemoveDraftEventHandler(ContentWorkspace workspace, IContentDrafts drafts)
{
    public async Task<AppResult<ContentDraftView>> Handle(RemoveDraftEvent command, CancellationToken cancellationToken)
        => await DraftEdits.SaveAsync(workspace, drafts, command.Revision, command.Actor, ContentAuditActions.DraftEventRemove, command.Id, draft =>
        {
            var before = DraftDocuments.Event(draft.EventsJson, command.Id);
            var events = DraftDocuments.RemoveEvent(draft.EventsJson, command.Id);
            return events is null ? null : (draft.BalanceJson, events, before, null);
        }, cancellationToken);
}

public sealed class PutDraftBalanceHandler(ContentWorkspace workspace, IContentDrafts drafts)
{
    public async Task<AppResult<ContentDraftView>> Handle(PutDraftBalance command, CancellationToken cancellationToken)
    {
        if (command.Balance.ValueKind != JsonValueKind.Object)
        {
            return AppResult<ContentDraftView>.Fail(ErrorCodes.ValidationFailed, "the balance must be a JSON object");
        }

        return await DraftEdits.SaveAsync(workspace, drafts, command.Revision, command.Actor, ContentAuditActions.DraftBalance, "balance", draft =>
        {
            var balance = DraftDocuments.Write(System.Text.Json.Nodes.JsonNode.Parse(command.Balance.GetRawText())!);
            return (balance, draft.EventsJson, draft.BalanceJson, balance);
        }, cancellationToken);
    }
}

/// <summary>One audited edit of the draft at the revision the owner read.</summary>
internal static class DraftEdits
{
    public static async Task<AppResult<ContentDraftView>> SaveAsync(
        ContentWorkspace workspace,
        IContentDrafts drafts,
        int revision,
        string actor,
        string action,
        string target,
        Func<ContentDraft, (string Balance, string Events, string? Before, string? After)?> edit,
        CancellationToken cancellationToken)
    {
        var draft = await drafts.FindAsync(cancellationToken);
        if (draft is null)
        {
            return AppResult<ContentDraftView>.Fail(ErrorCodes.DraftNotFound);
        }

        if (draft.Revision != revision)
        {
            return AppResult<ContentDraftView>.Fail(ErrorCodes.DraftStale, $"current-{draft.Revision}");
        }

        (string Balance, string Events, string? Before, string? After)? changed;
        try
        {
            changed = edit(draft);
        }
        catch (JsonException error)
        {
            return AppResult<ContentDraftView>.Fail(ErrorCodes.ValidationFailed, error.Message);
        }

        if (changed is not { } next)
        {
            return AppResult<ContentDraftView>.Fail(ErrorCodes.EventNotFound);
        }

        var saved = await drafts.SaveAsync(revision, next.Balance, next.Events, workspace.Audit(actor, action, target, next.Before, next.After), cancellationToken);
        return saved is null
            ? AppResult<ContentDraftView>.Fail(ErrorCodes.DraftStale)
            : AppResult<ContentDraftView>.Ok(await workspace.ViewAsync(saved, cancellationToken));
    }
}

public sealed class SimulateContentDraftHandler(ContentWorkspace workspace, IContentDrafts drafts, ContentSimulation simulation, ICurrentContent current)
{
    public async Task<AppResult<SimulationView>> Handle(SimulateContentDraft query, CancellationToken cancellationToken)
    {
        var draft = await drafts.FindAsync(cancellationToken);
        if (draft is null)
        {
            return AppResult<SimulationView>.Fail(ErrorCodes.DraftNotFound);
        }

        var (snapshot, problems) = await workspace.CheckAsync(draft.Files, cancellationToken);
        if (snapshot is null)
        {
            return AppResult<SimulationView>.Fail(ErrorCodes.ContentInvalid, string.Join(", ", problems));
        }

        var candidate = simulation.Run(snapshot);
        var baseline = simulation.Run(current.Snapshot);
        return AppResult<SimulationView>.Ok(new SimulationView(candidate, baseline, candidate.Violations.Count == 0 && candidate.Errors.Count == 0));
    }
}

public sealed class PublishContentDraftHandler(ContentWorkspace workspace, IContentDrafts drafts)
{
    public async Task<AppResult<ContentPublicationView>> Handle(PublishContentDraft command, CancellationToken cancellationToken)
    {
        if (AuditActions.Reason(command.Reason) is not { } reason)
        {
            return AppResult<ContentPublicationView>.Fail(ErrorCodes.ValidationFailed, $"reason must have 1-{AuditActions.MaxReasonLength} characters");
        }

        var draft = await drafts.FindAsync(cancellationToken);
        if (draft is null)
        {
            return AppResult<ContentPublicationView>.Fail(ErrorCodes.DraftNotFound);
        }

        if (draft.Revision != command.Revision)
        {
            return AppResult<ContentPublicationView>.Fail(ErrorCodes.DraftStale, $"current-{draft.Revision}");
        }

        return await workspace.PublishAsync(draft.Files, PublicationKind.Publish, null, draft.Revision, reason, command.Actor, cancellationToken);
    }
}

public sealed class RevertContentPublicationHandler(ContentWorkspace workspace, IContentPublications publications)
{
    public async Task<AppResult<ContentPublicationView>> Handle(RevertContentPublication command, CancellationToken cancellationToken)
    {
        if (AuditActions.Reason(command.Reason) is not { } reason)
        {
            return AppResult<ContentPublicationView>.Fail(ErrorCodes.ValidationFailed, $"reason must have 1-{AuditActions.MaxReasonLength} characters");
        }

        var source = await publications.FindAsync(command.Number, cancellationToken);
        if (source is null)
        {
            return AppResult<ContentPublicationView>.Fail(ErrorCodes.PublicationNotFound);
        }

        var files = await workspace.FilesAsync(source.Digest, cancellationToken);
        return await workspace.PublishAsync(files, PublicationKind.Revert, source.Number, null, reason, command.Actor, cancellationToken);
    }
}

public sealed class ListContentPublicationsHandler(IContentPublications publications)
{
    public async Task<IReadOnlyList<ContentPublicationView>> Handle(ListContentPublications query, CancellationToken cancellationToken)
        => [.. (await publications.ListAsync(cancellationToken)).Select(ContentWorkspace.View)];
}

public sealed class ExportContentPublicationHandler(ContentWorkspace workspace, IContentPublications publications)
{
    public async Task<AppResult<ContentExportView>> Handle(ExportContentPublication query, CancellationToken cancellationToken)
    {
        var publication = await publications.FindAsync(query.Number, cancellationToken);
        if (publication is null)
        {
            return AppResult<ContentExportView>.Fail(ErrorCodes.PublicationNotFound);
        }

        var files = await workspace.FilesAsync(publication.Digest, cancellationToken);
        return AppResult<ContentExportView>.Ok(new ContentExportView(
            publication.Number, publication.Digest, publication.MechanicsHash, Encoding.UTF8.GetString(files.Balance), Encoding.UTF8.GetString(files.Events)));
    }
}
