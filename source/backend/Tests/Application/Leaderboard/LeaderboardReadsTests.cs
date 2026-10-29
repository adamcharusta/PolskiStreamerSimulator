using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Leaderboard;

/// <summary>Spec M4-B §4 and §7: season tables, ranks and the season list.</summary>
public sealed class LeaderboardReadsTests
{
    private static readonly DateTimeOffset At = AppTestKit.Start;

    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_table_lists_the_visible_entries_of_one_season_in_order()
    {
        await kit.StartAsync();
        var hidden = Guid.NewGuid();
        await Add(Guid.NewGuid(), 100, 10, At);
        await Add(Guid.NewGuid(), 300, 10, At);
        await Add(hidden, 999, 10, At);
        await Add(Guid.NewGuid(), 200, 10, At, season: 2);
        kit.EntryStore.Hide(hidden, At);

        var board = (await kit.LeaderboardHandler().Handle(new GetLeaderboard(null, null), Ct)).Value;

        Assert.Equal(1, board.Season.Number);
        Assert.True(board.Season.Current);
        Assert.Equal([(1, 300L), (2, 100L)], board.Entries.Select(e => (e.Rank, e.Score)));
    }

    [Fact]
    public async Task The_limit_is_clamped_to_one_hundred()
    {
        await kit.StartAsync();
        for (var i = 0; i < 105; i++)
        {
            await Add(Guid.NewGuid(), i, 1, At);
        }

        Assert.Equal(100, (await kit.LeaderboardHandler().Handle(new GetLeaderboard(1, 500), Ct)).Value.Entries.Count);
        Assert.Single((await kit.LeaderboardHandler().Handle(new GetLeaderboard(1, 0), Ct)).Value.Entries);
    }

    [Fact]
    public async Task A_rank_outside_the_top_hundred_is_still_reported()
    {
        await kit.StartAsync();
        for (var i = 0; i < 120; i++)
        {
            await Add(Guid.NewGuid(), 1000 + i, 1, At);
        }

        var last = LeaderboardRulesTests.Entry(Guid.NewGuid(), 1, 1, At);
        await kit.Entries.AddAsync(last, Ct);

        Assert.Equal(121, await kit.Entries.RankAsync(last, Ct));
    }

    [Fact]
    public async Task An_unknown_season_is_not_found()
    {
        await kit.StartAsync();

        Assert.Equal(ErrorCodes.SeasonNotFound, (await kit.LeaderboardHandler().Handle(new GetLeaderboard(7, null), Ct)).Error.Code);
    }

    [Fact]
    public async Task New_content_closes_the_season_and_opens_the_next()
    {
        var mixed = AppTestKit.WithContent(TestPaths.ContentM1);
        await mixed.StartAsync();
        mixed.Time.Now = At.AddDays(3);
        mixed.SwitchCurrentContent(TestPaths.ContentM3);
        await mixed.StartAsync();
        await mixed.StartAsync();

        var seasons = await mixed.SeasonsHandler().Handle(ListSeasons.Instance, Ct);

        Assert.Equal([2, 1], seasons.Select(s => s.Number));
        Assert.Equal(new SeasonView(2, At.AddDays(3), null, null, false, true), seasons[0]);
        Assert.Equal(new SeasonView(1, At, At.AddDays(3), At.AddDays(93), false, false), seasons[1]);
    }

    private Task<bool> Add(Guid careerId, long score, int weeks, DateTimeOffset at, int season = 1)
        => kit.Entries.AddAsync(LeaderboardRulesTests.Entry(careerId, score, weeks, at, season), Ct);
}
