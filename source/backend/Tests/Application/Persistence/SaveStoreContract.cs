using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Application.Saves;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Persistence;

/// <summary>Slot and line-of-play rules every career and slot store pair must keep (spec M4-C §3–§5).</summary>
public abstract class SaveStoreContract
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A kit whose handlers use the career, content and slot stores under test.</summary>
    protected abstract Task<AppTestKit> CreateKitAsync();

    [Fact]
    public async Task The_three_slots_are_independent()
    {
        var kit = await CreateKitAsync();
        var created = await kit.NewCareerAsync();
        Assert.True((await kit.SaveSlotAsync(created.CareerId, 1, created.Career.Version)).IsSuccess);
        var week2 = await kit.PlayManualWeekAsync(created);
        Assert.True((await kit.SaveSlotAsync(created.CareerId, 2, week2.Version)).IsSuccess);
        var week3 = await kit.PlayManualWeekAsync(week2);
        Assert.True((await kit.SaveSlotAsync(created.CareerId, 3, week3.Version)).IsSuccess);

        var overwritten = (await kit.SaveSlotAsync(created.CareerId, 2, week3.Version)).Value;

        Assert.Equal([1, 3, 3], overwritten.Slots.Select(s => s.Week!.Value));
        Assert.Equal([0, 2, 2], overwritten.Slots.Select(s => s.LastSettledWeek!.Value));
        Assert.Equal(overwritten.Slots, (await kit.SlotsAsync(created.CareerId)).Value.Slots);
    }

    [Fact]
    public async Task A_restore_rewinds_the_career_and_the_history_follows_each_line()
    {
        var kit = await CreateKitAsync();
        var created = await kit.NewCareerAsync();
        var week1 = await kit.PlayManualWeekAsync(created);
        await kit.SaveSlotAsync(created.CareerId, 1, week1.Version);
        var week2 = await kit.PlayManualWeekAsync(week1);
        var week3 = await kit.PlayManualWeekAsync(week2);
        await kit.SaveSlotAsync(created.CareerId, 2, week3.Version);
        var week3State = (await kit.Careers.FindAsync(created.CareerId, Ct))!.State;

        var restored = (await kit.RestoreAsync(created.CareerId, 1, week3.Version)).Value.Career;

        Assert.Equal((1, week3.Version + 1), (restored.LastSettledWeek, restored.Version));
        Assert.Equal(1, restored.LastReport!.Week);
        Assert.Equal([1], (await kit.HistoryAsync(created.CareerId)).Weeks.Select(w => w.Week));

        var branch = await kit.PlayManualWeekAsync(restored, MainAction.ManageCommunity);
        Assert.Equal(2, (await kit.GetAsync(created.CareerId)).LastReport!.Week);
        Assert.Equal(
            [MainAction.Rest, MainAction.ManageCommunity],
            (await kit.HistoryAsync(created.CareerId)).Weeks.Select(w => w.Plan.MainAction));

        var back = (await kit.RestoreAsync(created.CareerId, 2, branch.Version)).Value.Career;

        Assert.Equal(3, back.LastSettledWeek);
        var backState = (await kit.Careers.FindAsync(created.CareerId, Ct))!.State;
        Assert.Equal(StateHash.Of(week3State with { StateVersion = 0 }), StateHash.Of(backState with { StateVersion = 0 }));
        Assert.Equal(
            [MainAction.Rest, MainAction.Rest, MainAction.Rest],
            (await kit.HistoryAsync(created.CareerId)).Weeks.Select(w => w.Plan.MainAction));
        Assert.Equal(3, (await kit.GetAsync(created.CareerId)).LastReport!.Week);
    }

    [Fact]
    public async Task Restoring_an_active_state_clears_the_ending_date()
    {
        var kit = await CreateKitAsync();
        var created = await kit.NewCareerAsync();
        await kit.SaveSlotAsync(created.CareerId, 1, created.Career.Version);
        var retired = await kit.RetireAsync(created.Career);
        Assert.NotNull((await kit.Careers.FindAsync(created.CareerId, Ct))!.EndedAt);

        var restored = (await kit.RestoreAsync(created.CareerId, 1, retired.Version)).Value.Career;

        var stored = (await kit.Careers.FindAsync(created.CareerId, Ct))!;
        Assert.Equal(CareerStatus.Active, restored.Status);
        Assert.Null(stored.EndedAt);
        Assert.Equal(CareerStatus.Active, stored.State.Status);
    }

    [Fact]
    public async Task An_imported_ended_career_is_stored_with_its_ending_date()
    {
        var kit = await CreateKitAsync();
        var created = await kit.NewCareerAsync();
        await kit.RetireAsync(created.Career);
        var file = (await kit.ExportAsync(created.CareerId)).Value;

        var imported = (await kit.ImportAsync(SaveFileTests.Json(file))).Value;

        var stored = (await kit.Careers.FindAsync(imported.CareerId, Ct))!;
        Assert.Equal(CareerStatus.Ended, stored.State.Status);
        Assert.Equal(kit.Time.GetUtcNow(), stored.EndedAt);
        Assert.Null(stored.Season);
    }

    [Fact]
    public async Task Slots_are_deleted_with_their_career()
    {
        var kit = await CreateKitAsync();
        var created = await kit.NewCareerAsync();
        await kit.SaveSlotAsync(created.CareerId, 1, created.Career.Version);

        kit.Time.Now = AppTestKit.Start.AddMonths(7);
        Assert.Equal(1, await kit.Sweep().RunAsync(Ct));

        Assert.Empty(await kit.Slots.ListAsync(created.CareerId, Ct));
        Assert.Null(await kit.Slots.FindAsync(created.CareerId, 1, Ct));
    }
}

public sealed class InMemorySaveStoreTests : SaveStoreContract
{
    protected override Task<AppTestKit> CreateKitAsync() => Task.FromResult(new AppTestKit());
}
