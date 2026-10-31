using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Saves;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Saves;

/// <summary>Spec M4-C §3: manual slots of unranked careers, refused for ranked ones.</summary>
public sealed class SaveSlotTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_new_career_has_three_empty_slots()
    {
        var created = await kit.NewCareerAsync();

        var slots = (await kit.SlotsAsync(created.CareerId)).Value.Slots;

        Assert.Equal([1, 2, 3], slots.Select(s => s.Slot));
        Assert.All(slots, s => Assert.Equal(new SaveSlotView(s.Slot, true, null, null, null, null, null, null), s));
    }

    [Fact]
    public async Task A_saved_slot_shows_the_state_and_when_it_was_saved()
    {
        var created = await kit.NewCareerAsync();
        var played = await kit.PlayManualWeekAsync(created);
        kit.Time.Now = AppTestKit.Start.AddHours(1);

        var slot = (await kit.SaveSlotAsync(created.CareerId, 2, played.Version)).Value.Slots[1];

        Assert.Equal(
            new SaveSlotView(2, false, kit.Time.Now, played.Week, 1, played.Status, played.Stats.CashPln, played.Stats.Followers),
            slot);
    }

    [Fact]
    public async Task Ranked_careers_have_no_slots_or_export()
    {
        var ranked = await kit.NewCareerAsync(ranked: true);

        Assert.Equal(ErrorCodes.CareerRanked, (await kit.SlotsAsync(ranked.CareerId)).Error.Code);
        Assert.Equal(ErrorCodes.CareerRanked, (await kit.SaveSlotAsync(ranked.CareerId, 1, 0)).Error.Code);
        Assert.Equal(ErrorCodes.CareerRanked, (await kit.RestoreAsync(ranked.CareerId, 1, 0)).Error.Code);
        Assert.Equal(ErrorCodes.CareerRanked, (await kit.ExportAsync(ranked.CareerId)).Error.Code);
    }

    [Fact]
    public async Task A_wrong_secret_looks_like_an_unknown_career()
    {
        var created = await kit.NewCareerAsync();

        var listed = await kit.SlotsHandler().Handle(new ListSaveSlots(created.CareerId, "wrong"), Ct);
        var exported = await kit.ExportHandler().Handle(new ExportCareer(created.CareerId, null), Ct);

        Assert.Equal(ErrorCodes.CareerNotFound, listed.Error.Code);
        Assert.Equal(ErrorCodes.MissingSecret, exported.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task A_slot_outside_one_to_three_does_not_exist(int slot)
    {
        var created = await kit.NewCareerAsync();

        Assert.Equal(ErrorCodes.SlotNotFound, (await kit.SaveSlotAsync(created.CareerId, slot, 0)).Error.Code);
        Assert.Equal(ErrorCodes.SlotNotFound, (await kit.RestoreAsync(created.CareerId, slot, 0)).Error.Code);
    }

    [Fact]
    public async Task Save_and_restore_reject_a_stale_version()
    {
        var created = await kit.NewCareerAsync();
        await kit.SaveSlotAsync(created.CareerId, 1, 0);
        var played = await kit.PlayManualWeekAsync(created);

        var saved = await kit.SaveSlotAsync(created.CareerId, 2, 0);
        var restored = await kit.RestoreAsync(created.CareerId, 1, 0);

        Assert.Equal((ErrorCodes.StaleVersion, $"current-{played.Version}"), (saved.Error.Code, saved.Error.Detail));
        Assert.Equal(ErrorCodes.StaleVersion, restored.Error.Code);
        Assert.True((await kit.SlotsAsync(created.CareerId)).Value.Slots[1].Empty);
    }

    [Fact]
    public async Task An_empty_slot_cannot_be_restored()
    {
        var created = await kit.NewCareerAsync();

        Assert.Equal(ErrorCodes.SlotEmpty, (await kit.RestoreAsync(created.CareerId, 3, 0)).Error.Code);
        Assert.Equal(0, kit.Store.LogCount(created.CareerId));
    }

    [Fact]
    public async Task A_repeated_restore_returns_its_stored_answer()
    {
        var created = await kit.NewCareerAsync();
        await kit.SaveSlotAsync(created.CareerId, 1, 0);
        var played = await kit.PlayManualWeekAsync(created);
        var commandId = Guid.NewGuid();
        var first = (await kit.RestoreAsync(created.CareerId, 1, played.Version, commandId)).Value;
        var logged = kit.Store.LogCount(created.CareerId);

        var again = await kit.RestoreAsync(created.CareerId, 1, played.Version, commandId);
        var otherSlot = await kit.RestoreAsync(created.CareerId, 2, played.Version, commandId);

        Assert.Equal(first.Career.Version, again.Value.Career.Version);
        Assert.Equal(logged, kit.Store.LogCount(created.CareerId));
        Assert.Equal(ErrorCodes.CommandIdReused, otherSlot.Error.Code);
    }

    [Fact]
    public async Task A_command_sent_before_the_restore_is_stale_after_it()
    {
        var created = await kit.NewCareerAsync();
        await kit.SaveSlotAsync(created.CareerId, 1, 0);

        var restored = (await kit.RestoreAsync(created.CareerId, 1, 0)).Value.Career;
        var stale = await kit.SubmitAsync(created.CareerId, Guid.NewGuid(), new ActionInput(0, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null));

        Assert.Equal(1, restored.Version);
        Assert.Equal(ErrorCodes.StaleVersion, stale.Error.Code);
        Assert.Equal(1, (await kit.PlayManualWeekAsync(restored)).LastSettledWeek);
    }

    [Fact]
    public async Task Saving_leaves_retention_unchanged_and_a_restore_extends_it()
    {
        var created = await kit.NewCareerAsync();
        var expires = created.Career.ExpiresAt;
        kit.Time.Now = AppTestKit.Start.AddDays(10);

        await kit.SaveSlotAsync(created.CareerId, 1, 0);
        Assert.Equal(expires, (await kit.Store.FindAsync(created.CareerId, Ct))!.ExpiresAt);

        var restored = (await kit.RestoreAsync(created.CareerId, 1, 0)).Value.Career;
        var stored = (await kit.Store.FindAsync(created.CareerId, Ct))!;
        Assert.Equal(kit.Time.Now, stored.LastActionAt);
        Assert.Equal(kit.Time.Now.AddMonths(6), stored.ExpiresAt);
        Assert.Equal(stored.ExpiresAt, restored.ExpiresAt);
    }
}
