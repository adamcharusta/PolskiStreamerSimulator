using System.Text;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Saves;

// Summaries of contract records stay on one line: the OpenAPI document copies them, and line breaks differ by OS.

/// <summary>One manual slot: empty, or the week, cash and followers of the saved state and when it was saved.</summary>
public sealed record SaveSlotView(int Slot, bool Empty, DateTimeOffset? SavedAt, int? Week, int? LastSettledWeek, CareerStatus? Status, long? CashPln, long? Followers);

/// <summary>The three manual slots of an unranked career, in slot order.</summary>
public sealed record SaveSlotsView(IReadOnlyList<SaveSlotView> Slots);

public sealed record ListSaveSlots(Guid CareerId, string? Secret)
{
    // Wolverine logs a failed message with ToString(); the bearer secret must never reach a log.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***");
        return true;
    }
}

public sealed record SaveToSlot(Guid CareerId, string? Secret, int Slot, long ExpectedVersion)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***, Slot = {Slot}, ExpectedVersion = {ExpectedVersion}");
        return true;
    }
}

public sealed record RestoreSlot(Guid CareerId, string? Secret, int Slot, Guid CommandId, long ExpectedVersion)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***, Slot = {Slot}, CommandId = {CommandId}, ExpectedVersion = {ExpectedVersion}");
        return true;
    }
}

/// <summary>The stored request of a restore; a repeat must send the same slot and version.</summary>
public sealed record RestoreRequest(int Slot, long ExpectedVersion);

/// <summary>Slot rules shared by the handlers (spec M4-C §3).</summary>
public static class SaveSlots
{
    public const int Count = 3;

    public static bool Exists(int slot) => slot is >= 1 and <= Count;

    /// <summary>Opens a career with its secret and admits only an unranked one.</summary>
    public static async Task<AppResult<StoredCareer>> OpenUnrankedAsync(CareerAccess access, Guid careerId, string? secret, CancellationToken cancellationToken)
    {
        var opened = await access.OpenAsync(careerId, secret, cancellationToken);
        return opened.IsSuccess && opened.Value.Season is not null ? AppResult<StoredCareer>.Fail(ErrorCodes.CareerRanked) : opened;
    }

    public static async Task<SaveSlotsView> ViewAsync(ISaveSlotStore slots, Guid careerId, CancellationToken cancellationToken)
    {
        var filled = (await slots.ListAsync(careerId, cancellationToken)).ToDictionary(s => s.Number);
        return new SaveSlotsView([.. Enumerable.Range(1, Count).Select(n => filled.TryGetValue(n, out var slot) ? View(slot) : Empty(n))]);
    }

    private static SaveSlotView Empty(int number) => new(number, true, null, null, null, null, null, null);

    private static SaveSlotView View(SaveSlot slot)
        => new(slot.Number, false, slot.SavedAt, slot.State.Week.Value, slot.State.LastSettledWeek, slot.State.Status, slot.State.CashPln, slot.State.Followers);
}

public sealed class ListSaveSlotsHandler(CareerAccess access, ISaveSlotStore slots)
{
    public async Task<AppResult<SaveSlotsView>> Handle(ListSaveSlots query, CancellationToken cancellationToken)
    {
        var opened = await SaveSlots.OpenUnrankedAsync(access, query.CareerId, query.Secret, cancellationToken);
        return opened.IsSuccess
            ? AppResult<SaveSlotsView>.Ok(await SaveSlots.ViewAsync(slots, opened.Value.Id, cancellationToken))
            : AppResult<SaveSlotsView>.Fail(opened.Error);
    }
}

/// <summary>Writes the state the player is looking at into a slot; not a gameplay action, so retention is unchanged.</summary>
public sealed class SaveToSlotHandler(CareerAccess access, ISaveSlotStore slots, TimeProvider time)
{
    public async Task<AppResult<SaveSlotsView>> Handle(SaveToSlot command, CancellationToken cancellationToken)
    {
        var opened = await SaveSlots.OpenUnrankedAsync(access, command.CareerId, command.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<SaveSlotsView>.Fail(opened.Error);
        }

        if (!SaveSlots.Exists(command.Slot))
        {
            return AppResult<SaveSlotsView>.Fail(ErrorCodes.SlotNotFound);
        }

        var career = opened.Value;
        if (career.State.StateVersion != command.ExpectedVersion)
        {
            return AppResult<SaveSlotsView>.Fail(ErrorCodes.StaleVersion, $"current-{career.State.StateVersion}");
        }

        await slots.SaveAsync(career.Id, command.Slot, career.State, time.GetUtcNow(), cancellationToken);
        return AppResult<SaveSlotsView>.Ok(await SaveSlots.ViewAsync(slots, career.Id, cancellationToken));
    }
}

/// <summary>
/// Replaces the state with a slot's state at the next version and logs one <see cref="ActionKind.Restore"/>, so stale
/// commands fail and a repeated restore returns its stored answer (spec M4-C §3).
/// </summary>
public sealed class RestoreSlotHandler(
    CareerAccess access,
    ICareerStore store,
    ISaveSlotStore slots,
    IContentStore contents,
    CareerViewBuilder views,
    TimeProvider time)
{
    public async Task<AppResult<ActionOutcome>> Handle(RestoreSlot command, CancellationToken cancellationToken)
    {
        var opened = await SaveSlots.OpenUnrankedAsync(access, command.CareerId, command.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<ActionOutcome>.Fail(opened.Error);
        }

        if (!SaveSlots.Exists(command.Slot))
        {
            return AppResult<ActionOutcome>.Fail(ErrorCodes.SlotNotFound);
        }

        var requestJson = ContractJson.Serialize(new RestoreRequest(command.Slot, command.ExpectedVersion));
        if (await RepeatAsync(command, requestJson, cancellationToken) is { } repeat)
        {
            return repeat;
        }

        var career = opened.Value;
        if (career.State.StateVersion != command.ExpectedVersion)
        {
            return AppResult<ActionOutcome>.Fail(ErrorCodes.StaleVersion, $"current-{career.State.StateVersion}");
        }

        if (await slots.FindAsync(career.Id, command.Slot, cancellationToken) is not { } slot)
        {
            return AppResult<ActionOutcome>.Fail(ErrorCodes.SlotEmpty);
        }

        var next = slot.State with { StateVersion = career.State.StateVersion + 1 };
        var content = await contents.RequireAsync(next.ContentDigest, cancellationToken);
        var lastReport = await store.FindReportOnLineAsync(career.Id, slot.LogSequence, cancellationToken) is { ReportJson: { } json }
            ? StoredContract.ReadReport(json)
            : null;
        var now = time.GetUtcNow();
        var view = views.Build(career.Id, career.Nickname, next, content, lastReport, CareerRetention.ExpiresAt(next, now), career.Season);
        var outcome = new ActionOutcome(view, null, null);
        var entry = new NewAction(command.CommandId, 0, ActionKind.Restore, requestJson, ContractJson.Serialize(outcome), null, slot.LogSequence);
        if (await store.AppendAsync(career.Id, command.ExpectedVersion, next, entry, now, cancellationToken) == AppendOutcome.Appended)
        {
            return AppResult<ActionOutcome>.Ok(outcome);
        }

        // A concurrent request won: the same restore (answer with its stored result) or another command (a stale version).
        return await RepeatAsync(command, requestJson, cancellationToken)
            ?? AppResult<ActionOutcome>.Fail(ErrorCodes.StaleVersion);
    }

    private async Task<AppResult<ActionOutcome>?> RepeatAsync(RestoreSlot command, string requestJson, CancellationToken cancellationToken)
    {
        var stored = await store.FindCommandAsync(command.CareerId, command.CommandId, cancellationToken);
        if (stored.Count == 0)
        {
            return null;
        }

        return stored is [{ Kind: ActionKind.Restore } only] && string.Equals(only.RequestJson, requestJson, StringComparison.Ordinal)
            ? AppResult<ActionOutcome>.Ok(StoredContract.ReadOutcome(only.ResponseJson))
            : AppResult<ActionOutcome>.Fail(ErrorCodes.CommandIdReused);
    }
}
