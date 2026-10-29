using System.Text;
using System.Text.Json;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Saves;

/// <summary>An exported unranked career: the canonical state without history; unsigned, validated on import (spec M4-C §5).</summary>
public sealed record SaveFile(string Format, int Schema, DateTimeOffset ExportedAt, string Nickname, JsonElement State);

public sealed record ExportCareer(Guid CareerId, string? Secret)
{
    // Wolverine logs a failed message with ToString(); the bearer secret must never reach a log.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***");
        return true;
    }
}

/// <summary>A save file to turn into a new unranked career.</summary>
public sealed record ImportCareer(JsonElement Save)
{
    // The file is player data of any size; the log line names only its JSON kind.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Save = {Save.ValueKind}");
        return true;
    }
}

public static class SaveFiles
{
    public const string Format = "polski-streamer-simulator-save";

    /// <summary>The export schema this server writes and the newest it reads.</summary>
    public const int Schema = 1;

    /// <summary>The largest import request body, in bytes.</summary>
    public const int MaxBytes = 64 * 1024;

    public static SaveFile Of(StoredCareer career, DateTimeOffset at)
    {
        using var state = JsonDocument.Parse(CanonicalJson.Serialize(career.State));
        return new SaveFile(Format, Schema, at, career.Nickname, state.RootElement.Clone());
    }
}

/// <summary>Exports an unranked career; a read, so retention is unchanged.</summary>
public sealed class ExportCareerHandler(CareerAccess access, TimeProvider time)
{
    public async Task<AppResult<SaveFile>> Handle(ExportCareer query, CancellationToken cancellationToken)
    {
        var opened = await SaveSlots.OpenUnrankedAsync(access, query.CareerId, query.Secret, cancellationToken);
        return opened.IsSuccess
            ? AppResult<SaveFile>.Ok(SaveFiles.Of(opened.Value, time.GetUtcNow()))
            : AppResult<SaveFile>.Fail(opened.Error);
    }
}

/// <summary>
/// Creates a new unranked career from a save file after every check of spec M4-C §5 passed; nothing is stored before,
/// so a rejected file cannot change any career or slot.
/// </summary>
public sealed class ImportCareerHandler(
    ICareerStore store,
    IContentStore contents,
    WeekEngine engine,
    ISecretHasher secrets,
    CareerViewBuilder views,
    TimeProvider time)
{
    public async Task<AppResult<CreatedCareer>> Handle(ImportCareer command, CancellationToken cancellationToken)
    {
        var read = Read(command.Save);
        if (!read.IsSuccess)
        {
            return AppResult<CreatedCareer>.Fail(read.Error);
        }

        var (nickname, state) = read.Value;
        if (!engine.Supports(state.RulesetId)
            || await contents.FindAsync(state.ContentDigest, cancellationToken) is not { } snapshot
            || snapshot.RulesetId != state.RulesetId)
        {
            return AppResult<CreatedCareer>.Fail(ErrorCodes.ImportUnsupported);
        }

        var now = time.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        if (state.StateVersion < 0 || CareerInvariants.Validate(state).Count > 0 || Viewable(id, nickname, state, snapshot, now) is not { } view)
        {
            return AppResult<CreatedCareer>.Fail(ErrorCodes.ImportInvalid, "the state is not a valid career");
        }

        var secret = secrets.NewSecret();
        var recoveryKey = RecoveryKeys.Format(id, secrets.NewSecret());
        await store.CreateAsync(new NewCareer(id, nickname, state, secrets.Hash(secret), secrets.Hash(recoveryKey), null, now), cancellationToken);
        return AppResult<CreatedCareer>.Ok(new CreatedCareer(id, secret, recoveryKey, view));
    }

    /// <summary>The envelope checks in order: format, schema, nickname, then the state itself.</summary>
    private static AppResult<(string Nickname, CareerState State)> Read(JsonElement save)
    {
        if (save.ValueKind != JsonValueKind.Object || Text(save, "format") != SaveFiles.Format || Number(save, "schema") is not { } schema)
        {
            return AppResult<(string, CareerState)>.Fail(ErrorCodes.ImportInvalid, "not a save file");
        }

        if (schema > SaveFiles.Schema)
        {
            return AppResult<(string, CareerState)>.Fail(ErrorCodes.ImportNewer, $"schema {schema} is newer than {SaveFiles.Schema}");
        }

        if (schema < SaveFiles.Schema)
        {
            return AppResult<(string, CareerState)>.Fail(ErrorCodes.ImportInvalid, $"schema {schema} is not supported");
        }

        if (Text(save, "nickname") is not { } nickname || !Nicknames.IsValid(nickname))
        {
            return AppResult<(string, CareerState)>.Fail(ErrorCodes.ImportInvalid, Nicknames.Rule);
        }

        return save.TryGetProperty("state", out var raw) && raw.ValueKind == JsonValueKind.Object && Deserialize(raw) is { } state
            ? AppResult<(string, CareerState)>.Ok((Nicknames.Normalize(nickname), state))
            : AppResult<(string, CareerState)>.Fail(ErrorCodes.ImportInvalid, "the state does not deserialize");
    }

    private static string? Text(JsonElement save, string name)
        => save.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Number(JsonElement save, string name)
        => save.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private static CareerState? Deserialize(JsonElement raw)
    {
        try
        {
            return CanonicalJson.Deserialize(Encoding.UTF8.GetBytes(raw.GetRawText()));
        }
        catch (Exception error) when (error is JsonException or NotSupportedException or FormatException or OverflowException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The view of the imported state, or <c>null</c> when its content references do not resolve.</summary>
    private CareerView? Viewable(Guid id, string nickname, CareerState state, ContentSnapshot snapshot, DateTimeOffset now)
    {
        try
        {
            return views.Build(id, nickname, state, snapshot, null, CareerRetention.ExpiresAt(state, now), null);
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            return null;
        }
    }
}
