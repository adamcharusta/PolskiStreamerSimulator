using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.Application.Leaderboard;

/// <summary>Spec M4-B §4, §5 and §7: nickname rule, submission deadline and leaderboard order.</summary>
public sealed class LeaderboardRulesTests
{
    private static readonly DateTimeOffset Closed = new(2027, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("A", "A")]
    [InlineData("ab12", "AB12")]
    [InlineData("  żółw ", "ŻÓŁW")]
    [InlineData("ĄĆĘŁŃ", "ĄĆĘŁŃ")]
    public void A_valid_nickname_is_trimmed_and_uppercased(string input, string stored)
    {
        Assert.True(LeaderboardNicknames.IsValid(input));
        Assert.Equal(stored, LeaderboardNicknames.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SIXSIX")]
    [InlineData("A B")]
    [InlineData("<b>")]
    [InlineData("É")]
    [InlineData("A_1")]
    public void An_invalid_nickname_is_rejected(string? input) => Assert.False(LeaderboardNicknames.IsValid(input));

    [Fact]
    public void A_decomposed_polish_letter_counts_as_one_character()
        => Assert.Equal("ŻABA", LeaderboardNicknames.Normalize("Żaba"));

    [Fact]
    public void An_open_season_has_no_deadline_and_accepts()
    {
        var season = new Season(1, "r", "d", Closed.AddYears(-1), null);

        Assert.Null(season.SubmissionDeadline);
        Assert.True(season.AcceptsAt(Closed.AddYears(5)));
        Assert.True(season.Pins("r", "d"));
        Assert.False(season.Pins("r", "other"));
    }

    [Fact]
    public void A_closed_season_accepts_for_ninety_days()
    {
        var season = new Season(1, "r", "d", Closed.AddYears(-1), Closed);

        Assert.Equal(Closed.AddDays(90), season.SubmissionDeadline);
        Assert.True(season.AcceptsAt(Closed.AddDays(90)));
        Assert.False(season.AcceptsAt(Closed.AddDays(90).AddTicks(1)));
        Assert.False(season.Pins("r", "d"));
    }

    [Fact]
    public void An_extension_moves_the_deadline_only_later()
    {
        var later = new Season(1, "r", "d", Closed.AddYears(-1), Closed, Closed.AddDays(100));
        var earlier = new Season(1, "r", "d", Closed.AddYears(-1), Closed, Closed.AddDays(10));

        Assert.Equal(Closed.AddDays(100), later.SubmissionDeadline);
        Assert.Equal(Closed.AddDays(90), earlier.SubmissionDeadline);
    }

    [Fact]
    public void Entries_order_by_score_then_weeks_then_publication_then_career()
    {
        // .NET orders these two the other way round.
        var low = Guid.Parse("00000001-0000-0000-0000-000000000000");
        var high = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var entries = new[]
        {
            Entry(low, 500, 100, Closed),
            Entry(Guid.NewGuid(), 900, 900, Closed),
            Entry(Guid.NewGuid(), 500, 50, Closed),
            Entry(high, 500, 100, Closed),
            Entry(Guid.NewGuid(), 500, 100, Closed.AddSeconds(-1)),
        };

        var ordered = entries.Order(LeaderboardOrder.Instance).ToList();

        Assert.Equal([900, 500, 500, 500, 500], ordered.Select(e => e.Score));
        Assert.Equal(50, ordered[1].WeeksPlayed);
        Assert.Equal(Closed.AddSeconds(-1), ordered[2].SubmittedAt);

        // SQL Server compares the last six bytes of a uniqueidentifier first.
        Assert.Equal([low, high], ordered.Skip(3).Select(e => e.CareerId));
    }

    internal static LeaderboardEntry Entry(Guid careerId, long score, int weeks, DateTimeOffset at, int season = 1)
        => new(Guid.NewGuid(), season, careerId, "AAA", score, weeks, Difficulty.Normal, EndingReason.Completed, "r", 1, new string('0', 64), at);
}
