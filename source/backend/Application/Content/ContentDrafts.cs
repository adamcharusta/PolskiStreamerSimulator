using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using PolskiStreamerSimulator.Application.Moderation;
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Application.Content;

/// <summary>The owner's single draft: both documents as JSON text, the publication it copied and its revision (spec M4-E §5).</summary>
public sealed record ContentDraft(string BalanceJson, string EventsJson, int BaseNumber, int Revision, DateTimeOffset UpdatedAt, string UpdatedBy)
{
    public ContentFiles Files => new(Encoding.UTF8.GetBytes(BalanceJson), Encoding.UTF8.GetBytes(EventsJson));
}

/// <summary>Kind of a publication: the repository content, a draft, or a copy of an earlier publication.</summary>
public enum PublicationKind
{
    Release,
    Publish,
    Revert,
}

/// <summary>A content version new careers pinned from <see cref="PublishedAt"/> until the next publication.</summary>
public sealed record ContentPublication(
    int Number,
    string Digest,
    string MechanicsHash,
    PublicationKind Kind,
    int? SourceNumber,
    string Actor,
    string Reason,
    DateTimeOffset PublishedAt);

/// <summary>A publication to store; the store assigns its number.</summary>
public sealed record NewPublication(string Digest, string MechanicsHash, PublicationKind Kind, int? SourceNumber, string Actor, string Reason, DateTimeOffset At);

/// <summary>Persistence port for the owner's draft; every change is stored with its audit record.</summary>
public interface IContentDrafts
{
    Task<ContentDraft?> FindAsync(CancellationToken cancellationToken);

    /// <summary>Stores a new draft at revision 1; <c>false</c> when one exists.</summary>
    Task<bool> CreateAsync(ContentDraft draft, AuditEntry audit, CancellationToken cancellationToken);

    /// <summary>Replaces the documents while the draft is at <paramref name="expectedRevision"/>; the revision moves on by one.</summary>
    Task<ContentDraft?> SaveAsync(int expectedRevision, string balanceJson, string eventsJson, AuditEntry audit, CancellationToken cancellationToken);

    /// <summary>Deletes the draft while it is at <paramref name="expectedRevision"/>.</summary>
    Task<bool> DeleteAsync(int expectedRevision, AuditEntry audit, CancellationToken cancellationToken);
}

/// <summary>Persistence port for the content new careers pin (spec M4-E §3).</summary>
public interface IContentPublications
{
    Task<ContentPublication?> LatestAsync(CancellationToken cancellationToken);

    Task<ContentPublication?> FindAsync(int number, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<ContentPublication>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores the publication with its audit record and, when <paramref name="draftRevision"/> is given, deletes the
    /// draft at that revision, in one transaction; <c>null</c> when the draft moved on and nothing was stored.
    /// </summary>
    Task<ContentPublication?> PublishAsync(NewPublication publication, AuditEntry audit, int? draftRevision, CancellationToken cancellationToken);

    /// <summary>Stores <paramref name="release"/> unless a release with its digest exists; returns the newest publication.</summary>
    Task<ContentPublication> EnsureReleasedAsync(NewPublication release, AuditEntry audit, CancellationToken cancellationToken);
}

/// <summary>Parses and validates content files the way the server loads them.</summary>
public interface IContentCompiler
{
    /// <summary>The snapshot, or <c>null</c> with every problem found.</summary>
    (ContentSnapshot? Snapshot, IReadOnlyList<string> Problems) Compile(ContentFiles files);
}

/// <summary>Edits of the draft documents; output is indented UTF-8 JSON with Polish letters unescaped.</summary>
public static class DraftDocuments
{
    private static readonly JsonSerializerOptions Output = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Write(JsonNode node) => node.ToJsonString(Output) + "\n";

    /// <summary>The events document with <paramref name="item"/> in place of the event with its ID, or appended.</summary>
    public static string PutEvent(string eventsJson, string id, JsonElement item)
    {
        var root = Root(eventsJson);
        var events = root["events"] as JsonArray ?? throw new JsonException("The events document has no events array.");
        var node = JsonNode.Parse(item.GetRawText());
        var index = IndexOf(events, id);
        if (index >= 0)
        {
            events[index] = node;
        }
        else
        {
            events.Add(node);
        }

        return Write(root);
    }

    /// <summary>The events document without the event, or <c>null</c> when it has none with that ID.</summary>
    public static string? RemoveEvent(string eventsJson, string id)
    {
        var root = Root(eventsJson);
        var events = root["events"] as JsonArray ?? throw new JsonException("The events document has no events array.");
        var index = IndexOf(events, id);
        if (index < 0)
        {
            return null;
        }

        events.RemoveAt(index);
        return Write(root);
    }

    /// <summary>The event with the ID as JSON text, or <c>null</c>.</summary>
    public static string? Event(string eventsJson, string id)
    {
        var events = Root(eventsJson)["events"] as JsonArray;
        var index = events is null ? -1 : IndexOf(events, id);
        return index < 0 ? null : events![index]!.ToJsonString(Output);
    }

    private static JsonObject Root(string json)
        => JsonNode.Parse(json) as JsonObject ?? throw new JsonException("The document is not a JSON object.");

    private static int IndexOf(JsonArray events, string id)
    {
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i]?["id"] is JsonValue value && value.TryGetValue<string>(out var existing) && existing == id)
            {
                return i;
            }
        }

        return -1;
    }
}
