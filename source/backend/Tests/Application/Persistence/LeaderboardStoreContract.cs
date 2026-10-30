using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Application.Moderation;
using PolskiStreamerSimulator.Tests.Application.Leaderboard;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Persistence;

/// <summary>Season and leaderboard rules every store implementation must keep (spec M4-B §4, §6, §7).</summary>
public abstract class LeaderboardStoreContract
{
    protected static readonly DateTimeOffset At = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Two content digests the stores may reference (the SQL Server store needs them stored).</summary>
    protected static readonly string M1 = TestContent.Of(TestPaths.ContentM1).Digest;

    protected static readonly string M3 = TestContent.Of(TestPaths.ContentM3).Digest;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected abstract Task<(ISeasonStore Seasons, ILeaderboardStore Entries)> CreateStoresAsync();

    protected static AuditEntry Audit(string reason, DateTimeOffset at)
        => new("test", AuditActions.Extend, "1", reason, null, null, at);

    [Fact]
    public async Task The_first_start_opens_season_one_and_a_restart_keeps_it()
    {
        var (seasons, _) = await CreateStoresAsync();

        var first = await seasons.EnsureCurrentAsync("m1", M1, M1, At, Ct);
        var again = await seasons.EnsureCurrentAsync("m1", M1, M1, At.AddDays(1), Ct);

        Assert.Equal(new Season(1, "m1", M1, At, null, MechanicsHash: M1), first);
        Assert.Equal(first, again);
        Assert.Equal(first, await seasons.CurrentAsync(Ct));
    }

    [Fact]
    public async Task New_content_closes_the_open_season_and_opens_the_next()
    {
        var (seasons, _) = await CreateStoresAsync();
        await seasons.EnsureCurrentAsync("m1", M1, M1, At, Ct);

        var next = await seasons.EnsureCurrentAsync("m3", M3, M3, At.AddDays(5), Ct);

        Assert.Equal(new Season(2, "m3", M3, At.AddDays(5), null, MechanicsHash: M3), next);
        Assert.Equal(new Season(1, "m1", M1, At, At.AddDays(5), MechanicsHash: M1), await seasons.FindAsync(1, Ct));
        Assert.Equal([2, 1], (await seasons.ListAsync(Ct)).Select(s => s.Number));
        Assert.Null(await seasons.FindAsync(3, Ct));
    }

    [Fact]
    public async Task Concurrent_starts_leave_one_open_season()
    {
        var (seasons, _) = await CreateStoresAsync();
        await seasons.EnsureCurrentAsync("m1", M1, M1, At, Ct);

        var started = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => seasons.EnsureCurrentAsync("m3", M3, M3, At.AddDays(1), Ct)));

        Assert.Single(started.Select(s => s.Number).Distinct());
        Assert.Single(await seasons.ListAsync(Ct), s => s.ClosedAt is null);
    }

    [Fact]
    public async Task The_latest_extension_is_the_deadline()
    {
        var (seasons, _) = await CreateStoresAsync();
        await seasons.EnsureCurrentAsync("m1", M1, M1, At, Ct);
        await seasons.EnsureCurrentAsync("m3", M3, M3, At.AddDays(1), Ct);

        await seasons.AddExtensionAsync(1, At.AddDays(120), Audit("Outage", At.AddDays(80)), Ct);
        await seasons.AddExtensionAsync(1, At.AddDays(150), Audit("Longer outage", At.AddDays(110)), Ct);

        Assert.Equal(At.AddDays(150), (await seasons.FindAsync(1, Ct))!.SubmissionDeadline);
        Assert.Null((await seasons.FindAsync(2, Ct))!.ExtendedUntil);
    }

    [Fact]
    public async Task A_career_has_at_most_one_entry()
    {
        var (seasons, entries) = await CreateStoresAsync();
        await seasons.EnsureCurrentAsync("m1", M1, M1, At, Ct);
        var careerId = Guid.NewGuid();
        var first = LeaderboardRulesTests.Entry(careerId, 100, 10, At);

        Assert.True(await entries.AddAsync(first, Ct));
        Assert.False(await entries.AddAsync(LeaderboardRulesTests.Entry(careerId, 900, 1, At.AddDays(1)), Ct));
        Assert.Equal(first, await entries.FindByCareerAsync(careerId, Ct));
    }

    [Fact]
    public async Task Ranks_and_the_table_follow_the_leaderboard_order()
    {
        var (seasons, entries) = await CreateStoresAsync();
        await seasons.EnsureCurrentAsync("m1", M1, M1, At, Ct);
        await seasons.EnsureCurrentAsync("m3", M3, M3, At, Ct);

        // .NET and SQL Server order these two career IDs differently; the stores agree with SQL Server.
        var tieA = LeaderboardRulesTests.Entry(Guid.Parse("00000001-0000-0000-0000-000000000000"), 500, 10, At);
        var tieB = LeaderboardRulesTests.Entry(Guid.Parse("00000000-0000-0000-0000-000000000001"), 500, 10, At);
        var top = LeaderboardRulesTests.Entry(Guid.NewGuid(), 900, 99, At);
        var fewerWeeks = LeaderboardRulesTests.Entry(Guid.NewGuid(), 500, 9, At);
        var earlier = LeaderboardRulesTests.Entry(Guid.NewGuid(), 500, 10, At.AddMinutes(-1));
        var otherSeason = LeaderboardRulesTests.Entry(Guid.NewGuid(), 5000, 1, At, season: 2);
        foreach (var e in new[] { tieB, otherSeason, earlier, tieA, top, fewerWeeks })
        {
            await entries.AddAsync(e, Ct);
        }

        var table = await entries.TopAsync(1, 100, Ct);

        Assert.Equal([top.CareerId, fewerWeeks.CareerId, earlier.CareerId, tieA.CareerId, tieB.CareerId], table.Select(e => e.CareerId));
        var ranks = await Task.WhenAll(table.Select(e => entries.RankAsync(e, Ct)));
        Assert.Equal([1, 2, 3, 4, 5], ranks);
        Assert.Equal(2, (await entries.TopAsync(1, 2, Ct)).Count);
        Assert.Equal(1, await entries.RankAsync(otherSeason, Ct));
    }
}

public sealed class InMemoryLeaderboardStoreTests : LeaderboardStoreContract
{
    protected override Task<(ISeasonStore Seasons, ILeaderboardStore Entries)> CreateStoresAsync()
        => Task.FromResult<(ISeasonStore, ILeaderboardStore)>((new InMemorySeasonStore(), new InMemoryLeaderboardStore()));
}
