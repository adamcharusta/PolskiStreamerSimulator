using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

/// <summary>Spec M4-A §6: six months after the last played step, 12 months after the ending; reads never extend.</summary>
public sealed class RetentionSweepTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unfinished_career_is_deleted_six_months_after_its_last_action()
    {
        var created = await kit.NewCareerAsync();
        var played = AppTestKit.Start.AddDays(20);
        kit.Time.Now = played;
        var view = await kit.PlayManualWeekAsync(created);
        Assert.Equal(played.AddMonths(6), view.ExpiresAt);

        kit.Time.Now = AppTestKit.Start.AddMonths(6);
        Assert.Equal(0, await kit.Sweep().RunAsync(Ct));

        kit.Time.Now = played.AddMonths(6);
        Assert.Equal(1, await kit.Sweep().RunAsync(Ct));
        Assert.Null(await kit.Store.FindAsync(created.CareerId, Ct));
        Assert.Equal(0, kit.Store.LogCount(created.CareerId));
    }

    [Fact]
    public async Task Reads_and_a_repeated_command_do_not_extend_the_expiry()
    {
        var created = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        var input = new ActionInput(created.Career.Version, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null);
        await kit.SubmitAsync(created.CareerId, command, input);

        kit.Time.Now = AppTestKit.Start.AddMonths(5);
        await kit.GetAsync(created.CareerId);
        await kit.HistoryHandler().Handle(new GetHistory(created.CareerId, created.Secret), Ct);
        Assert.True((await kit.SubmitAsync(created.CareerId, command, input)).IsSuccess);

        Assert.Equal(AppTestKit.Start.AddMonths(6), (await kit.Store.FindAsync(created.CareerId, Ct))!.ExpiresAt);
        Assert.Equal(AppTestKit.Start.AddMonths(6), (await kit.GetAsync(created.CareerId)).ExpiresAt);
    }

    [Fact]
    public async Task A_finished_career_is_kept_twelve_months_after_its_ending()
    {
        var created = await kit.NewCareerAsync();
        var ending = AppTestKit.Start.AddDays(3);
        kit.Time.Now = ending;
        var retire = new ActionInput(created.Career.Version, ActionType.Retire, null, null, null, null, null);
        var retired = await kit.SubmitAsync(created.CareerId, Guid.NewGuid(), retire);
        Assert.Equal(ending.AddMonths(12), retired.Value.Career.ExpiresAt);

        kit.Time.Now = ending.AddMonths(11);
        Assert.Equal(0, await kit.Sweep().RunAsync(Ct));
        kit.Time.Now = ending.AddMonths(12);
        Assert.Equal(1, await kit.Sweep().RunAsync(Ct));
    }

    [Fact]
    public async Task The_sweep_deletes_more_careers_than_one_batch()
    {
        for (var i = 0; i < RetentionSweep.BatchSize + 1; i++)
        {
            await kit.NewCareerAsync();
        }

        kit.Time.Now = AppTestKit.Start.AddMonths(6);

        Assert.Equal(RetentionSweep.BatchSize + 1, await kit.Sweep().RunAsync(Ct));
        Assert.Equal(0, kit.Store.Count);
    }
}
