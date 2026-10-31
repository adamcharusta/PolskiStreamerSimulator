using System.Text.Json;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Application.Moderation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Moderation;

/// <summary>Spec M4-D §5: hiding, restoring and the audit record of every owner change.</summary>
public sealed class ModerationTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<LeaderboardEntry> PublishedAsync(string nickname)
    {
        var created = await kit.NewCareerAsync(ranked: true);
        await kit.RetireAsync(created.Career);
        Assert.True((await kit.PublishAsync(created.CareerId, nickname)).IsSuccess);
        return (await kit.Entries.FindByCareerAsync(created.CareerId, Ct))!;
    }

    private Task<AppResult<ModeratedEntryView>> ModerateAsync(Guid entryId, bool hide, string? reason = "Obraźliwy nick")
        => kit.ModerateHandler().Handle(new ModerateEntry(entryId, hide, reason, "owner"), Ct);

    [Fact]
    public async Task A_hidden_entry_leaves_the_public_table_and_a_restore_brings_it_back_unchanged()
    {
        var entry = await PublishedAsync("ABC");
        kit.Time.Now = AppTestKit.Start.AddHours(1);

        var hidden = (await ModerateAsync(entry.Id, hide: true)).Value;

        Assert.Equal((kit.Time.Now, "Obraźliwy nick", (int?)null), (hidden.HiddenAt, hidden.HiddenReason, hidden.Rank));
        Assert.Empty((await kit.LeaderboardHandler().Handle(new GetLeaderboard(null, null), Ct)).Value.Entries);
        Assert.Null((await kit.EntryStatusAsync(entry.CareerId)).Entry!.Rank);

        var restored = (await ModerateAsync(entry.Id, hide: false, "Pomyłka")).Value;

        Assert.Equal((null, null, 1), (restored.HiddenAt, restored.HiddenReason, restored.Rank));
        var row = Assert.Single((await kit.LeaderboardHandler().Handle(new GetLeaderboard(null, null), Ct)).Value.Entries);
        Assert.Equal((entry.Score, entry.WeeksPlayed, entry.SubmittedAt), (row.Score, row.WeeksPlayed, row.SubmittedAt));
    }

    [Fact]
    public async Task Each_change_writes_one_audit_record_with_the_values_before_and_after()
    {
        var entry = await PublishedAsync("ABC");
        await ModerateAsync(entry.Id, hide: true);
        await ModerateAsync(entry.Id, hide: false, "Pomyłka");

        var audit = await kit.AuditHandler().Handle(new ListAudit(null), Ct);

        Assert.Equal([AuditActions.Restore, AuditActions.Hide], audit.Select(a => a.Action));
        var hide = audit[1];
        Assert.Equal(("owner", entry.Id.ToString("D"), "Obraźliwy nick"), (hide.Actor, hide.Target, hide.Reason));
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(hide.OldJson!).RootElement.GetProperty("hiddenAt").ValueKind);
        Assert.Equal("Obraźliwy nick", JsonDocument.Parse(hide.NewJson!).RootElement.GetProperty("hiddenReason").GetString());
    }

    [Fact]
    public async Task A_change_that_does_nothing_is_refused_and_not_audited()
    {
        var entry = await PublishedAsync("ABC");

        var restoreVisible = await ModerateAsync(entry.Id, hide: false);
        await ModerateAsync(entry.Id, hide: true);
        var hideHidden = await ModerateAsync(entry.Id, hide: true);
        var missing = await ModerateAsync(Guid.NewGuid(), hide: true);

        Assert.Equal(ErrorCodes.EntryVisible, restoreVisible.Error.Code);
        Assert.Equal(ErrorCodes.EntryHidden, hideHidden.Error.Code);
        Assert.Equal(ErrorCodes.EntryNotFound, missing.Error.Code);
        Assert.Single(kit.AuditStore.All);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_reason_is_required(string? reason)
    {
        var entry = await PublishedAsync("ABC");

        Assert.Equal(ErrorCodes.ValidationFailed, (await ModerateAsync(entry.Id, hide: true, reason)).Error.Code);
        Assert.Equal(ErrorCodes.ValidationFailed, (await ModerateAsync(entry.Id, hide: true, new string('a', 501))).Error.Code);
        Assert.Empty(kit.AuditStore.All);
    }

    [Fact]
    public async Task The_owner_sees_every_entry_with_public_ranks_for_the_visible_ones()
    {
        var first = await PublishedAsync("AAA");
        var second = await PublishedAsync("BBB");
        await ModerateAsync(first.Id, hide: true);

        var season = (await kit.ModeratedHandler().Handle(new ListModeratedEntries(null), Ct)).Value;

        Assert.Equal(2, season.Entries.Count);
        Assert.Equal([null, 1], season.Entries.OrderBy(e => e.Nickname).Select(e => e.Rank));
        Assert.All(season.Entries, e => Assert.Equal(64, e.FinalStateHash.Length));
        Assert.Equal(second.CareerId, season.Entries.Single(e => e.Rank == 1).CareerId);
        Assert.Equal(ErrorCodes.SeasonNotFound, (await kit.ModeratedHandler().Handle(new ListModeratedEntries(9), Ct)).Error.Code);
    }

    [Fact]
    public async Task A_season_extension_writes_an_audit_record_with_both_deadlines()
    {
        await kit.StartAsync();
        kit.SeasonStore.CloseCurrent(AppTestKit.Start);
        var until = AppTestKit.Start.AddDays(120);

        var extended = await kit.ExtendHandler().Handle(new ExtendSeason(1, until, " Awaria ", "owner"), Ct);

        Assert.True(extended.IsSuccess);
        var record = Assert.Single(kit.AuditStore.All).Entry;
        Assert.Equal((AuditActions.Extend, "1", "Awaria", "owner"), (record.Action, record.Target, record.Reason, record.Actor));
        Assert.Equal(AppTestKit.Start.AddDays(90), JsonDocument.Parse(record.OldJson!).RootElement.GetProperty("deadline").GetDateTimeOffset());
        Assert.Equal(until, JsonDocument.Parse(record.NewJson!).RootElement.GetProperty("deadline").GetDateTimeOffset());
    }

    [Fact]
    public async Task The_audit_list_is_newest_first_and_bounded()
    {
        var entry = await PublishedAsync("ABC");
        for (var i = 0; i < 3; i++)
        {
            await ModerateAsync(entry.Id, hide: true, $"hide {i}");
            await ModerateAsync(entry.Id, hide: false, $"restore {i}");
        }

        var two = await kit.AuditHandler().Handle(new ListAudit(2), Ct);

        Assert.Equal(["restore 2", "hide 2"], two.Select(a => a.Reason));
        Assert.Single(await kit.AuditHandler().Handle(new ListAudit(0), Ct));
    }
}
