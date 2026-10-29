using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Leaderboard;

/// <summary>Spec M4-B §3 and §6: ranked creation and the publication of a server-computed result.</summary>
public sealed class PublicationTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_ranked_career_pins_the_open_season()
    {
        var ranked = await kit.NewCareerAsync(ranked: true);
        var unranked = await kit.NewCareerAsync();

        Assert.True(ranked.Career.Ranked);
        Assert.Equal(1, ranked.Career.Season);
        Assert.Equal(1, (await kit.Store.FindAsync(ranked.CareerId, Ct))!.Season);
        Assert.False(unranked.Career.Ranked);
        Assert.Null(unranked.Career.Season);
        Assert.Equal(1, (await kit.GetAsync(ranked.CareerId)).Season);
    }

    [Fact]
    public async Task Ranked_creation_fails_while_the_open_season_has_other_content()
    {
        var mixed = AppTestKit.WithContent(TestPaths.ContentM1);
        await mixed.StartAsync();
        mixed.SwitchCurrentContent(TestPaths.ContentM3);
        await mixed.ContentStore.EnsureStoredAsync(mixed.Current.Files, Ct);

        var ranked = await mixed.CreateHandler().Handle(new CreateCareer("Streamer", Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman, Ranked: true), Ct);
        var unranked = await mixed.CreateHandler().Handle(new CreateCareer("Streamer", Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman), Ct);

        Assert.Equal(ErrorCodes.SeasonUnavailable, ranked.Error.Code);
        Assert.True(unranked.IsSuccess);
    }

    [Fact]
    public async Task A_repeat_with_another_ranked_choice_is_a_reused_creation_id()
    {
        await kit.StartAsync();
        var creationId = Guid.NewGuid();
        var first = new CreateCareer("Streamer", Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman, creationId, Ranked: true);

        Assert.True((await kit.CreateHandler().Handle(first, Ct)).IsSuccess);
        Assert.True((await kit.CreateHandler().Handle(first, Ct)).IsSuccess);
        Assert.Equal(ErrorCodes.CreationIdReused, (await kit.CreateHandler().Handle(first with { Ranked = false }, Ct)).Error.Code);
    }

    [Fact]
    public async Task A_retired_ranked_career_publishes_its_own_score()
    {
        var created = await kit.NewCareerAsync(ranked: true);
        var ended = await kit.RetireAsync(created.Career);
        Assert.Equal(EntryStatus.Open, (await kit.EntryStatusAsync(created.CareerId)).Status);
        kit.Time.Now = AppTestKit.Start.AddHours(1);

        var published = (await kit.PublishAsync(created.CareerId, " ab ")).Value;

        Assert.Equal(EntryStatus.Published, published.Status);
        Assert.Equal(1, published.Season);
        Assert.Equal(new PublishedEntryView("AB", ended.Score!.FinalScore, ended.LastSettledWeek, AppTestKit.Start.AddHours(1), 1), published.Entry);
        var stored = (await kit.Entries.FindByCareerAsync(created.CareerId, Ct))!;
        Assert.Equal(EndingReason.EarlyRetirement, stored.Ending);
        Assert.Equal(ended.Version, stored.FinalStateVersion);
        Assert.Equal(64, stored.FinalStateHash.Length);
    }

    [Fact]
    public async Task A_repeated_publication_returns_the_first_entry()
    {
        var created = await kit.NewCareerAsync(ranked: true);
        await kit.RetireAsync(created.Career);
        var first = (await kit.PublishAsync(created.CareerId, "ONE")).Value;

        kit.Time.Now = AppTestKit.Start.AddDays(1);
        var again = (await kit.PublishAsync(created.CareerId, "TWO")).Value;
        var invalid = (await kit.PublishAsync(created.CareerId, "not valid at all")).Value;

        Assert.Equal(first, again);
        Assert.Equal(first, invalid);
        Assert.Equal(1, kit.EntryStore.Count);
    }

    [Fact]
    public async Task An_unranked_career_cannot_publish()
    {
        var created = await kit.NewCareerAsync();
        await kit.RetireAsync(created.Career);

        Assert.Equal(EntryStatus.Unranked, (await kit.EntryStatusAsync(created.CareerId)).Status);
        Assert.Equal(ErrorCodes.CareerUnranked, (await kit.PublishAsync(created.CareerId, "AAA")).Error.Code);
    }

    [Fact]
    public async Task An_unfinished_career_cannot_publish()
    {
        var created = await kit.NewCareerAsync(ranked: true);

        Assert.Equal(EntryStatus.NotEnded, (await kit.EntryStatusAsync(created.CareerId)).Status);
        Assert.Equal(ErrorCodes.CareerNotEnded, (await kit.PublishAsync(created.CareerId, "AAA")).Error.Code);
        Assert.Equal(0, kit.EntryStore.Count);
    }

    [Fact]
    public async Task An_invalid_nickname_leaves_the_career_able_to_publish()
    {
        var created = await kit.NewCareerAsync(ranked: true);
        await kit.RetireAsync(created.Career);

        var rejected = await kit.PublishAsync(created.CareerId, "TOOLONG");

        Assert.Equal(ErrorCodes.ValidationFailed, rejected.Error.Code);
        Assert.Equal(LeaderboardNicknames.Rule, rejected.Error.Detail);
        Assert.True((await kit.PublishAsync(created.CareerId, "OK")).IsSuccess);
    }

    [Fact]
    public async Task A_closed_season_accepts_until_its_deadline_or_extension()
    {
        var created = await kit.NewCareerAsync(ranked: true);
        await kit.RetireAsync(created.Career);
        kit.SeasonStore.CloseCurrent(AppTestKit.Start.AddDays(1));

        kit.Time.Now = AppTestKit.Start.AddDays(92);
        var closed = await kit.EntryStatusAsync(created.CareerId);
        Assert.Equal(EntryStatus.Closed, closed.Status);
        Assert.Equal(AppTestKit.Start.AddDays(91), closed.SubmissionDeadline);
        Assert.Equal(ErrorCodes.SubmissionClosed, (await kit.PublishAsync(created.CareerId, "LATE")).Error.Code);

        var extended = await kit.ExtendHandler().Handle(new ExtendSeason(1, AppTestKit.Start.AddDays(100), "Ranking outage", "test"), Ct);
        Assert.True(extended.Value.Extended);
        Assert.Equal(EntryStatus.Open, (await kit.EntryStatusAsync(created.CareerId)).Status);
        Assert.True((await kit.PublishAsync(created.CareerId, "LATE")).IsSuccess);
    }

    [Fact]
    public async Task Only_a_later_deadline_of_a_closed_season_is_an_extension()
    {
        await kit.StartAsync();
        var open = await kit.ExtendHandler().Handle(new ExtendSeason(1, AppTestKit.Start.AddYears(1), "Outage", "test"), Ct);
        kit.SeasonStore.CloseCurrent(AppTestKit.Start);
        var earlier = await kit.ExtendHandler().Handle(new ExtendSeason(1, AppTestKit.Start.AddDays(30), "Outage", "test"), Ct);
        var missing = await kit.ExtendHandler().Handle(new ExtendSeason(9, AppTestKit.Start.AddYears(1), "Outage", "test"), Ct);
        var noReason = await kit.ExtendHandler().Handle(new ExtendSeason(1, AppTestKit.Start.AddYears(1), " ", "test"), Ct);

        Assert.Equal(ErrorCodes.ValidationFailed, open.Error.Code);
        Assert.Equal(ErrorCodes.ValidationFailed, earlier.Error.Code);
        Assert.Equal(ErrorCodes.SeasonNotFound, missing.Error.Code);
        Assert.Equal(ErrorCodes.ValidationFailed, noReason.Error.Code);
        Assert.Empty(kit.SeasonStore.Extensions);
    }

    [Fact]
    public async Task A_wrong_secret_reads_and_publishes_nothing()
    {
        var created = await kit.NewCareerAsync(ranked: true);
        await kit.RetireAsync(created.Career);

        var status = await kit.EntryStatusHandler().Handle(new GetEntryStatus(created.CareerId, "wrong"), Ct);
        var publish = await kit.PublishHandler().Handle(new PublishResult(created.CareerId, "wrong", "AAA"), Ct);

        Assert.Equal(ErrorCodes.CareerNotFound, status.Error.Code);
        Assert.Equal(ErrorCodes.CareerNotFound, publish.Error.Code);
        Assert.Equal(0, kit.EntryStore.Count);
    }

    [Fact]
    public async Task An_entry_outlives_the_retention_sweep_of_its_career()
    {
        var created = await kit.NewCareerAsync(ranked: true);
        await kit.RetireAsync(created.Career);
        await kit.PublishAsync(created.CareerId, "KEEP");

        kit.Time.Now = AppTestKit.Start.AddMonths(13);
        Assert.Equal(1, await kit.Sweep().RunAsync(Ct));

        var board = (await kit.LeaderboardHandler().Handle(new GetLeaderboard(1, null), Ct)).Value;
        Assert.Equal("KEEP", Assert.Single(board.Entries).Nickname);
    }

    [Fact]
    public async Task The_redacted_commands_never_print_the_secret()
    {
        Assert.DoesNotContain("s3cret", new PublishResult(Guid.NewGuid(), "s3cret", "AAA").ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("s3cret", new GetEntryStatus(Guid.NewGuid(), "s3cret").ToString(), StringComparison.Ordinal);
        await Task.CompletedTask;
    }
}
