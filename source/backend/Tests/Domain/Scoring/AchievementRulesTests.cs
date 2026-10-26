using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Scoring;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Scoring;

public class AchievementRulesTests
{
    private static AchievementDefinition Def(string id, AchievementKind kind, long? threshold, int points)
        => new(id, id, id, kind, threshold is { } t ? Fixed.FromInt(t) : null, points);

    public static readonly ImmutableArray<AchievementDefinition> Defs =
    [
        Def("fans", AchievementKind.Followers, 10_000, 1),
        Def("rich", AchievementKind.Cash, 1_000_000, 1),
        Def("done", AchievementKind.Completed, null, 2),
        Def("master", AchievementKind.Mastery, 90, 1),
        Def("one-category", AchievementKind.Specialist, 520, 1),
        Def("moved", AchievementKind.PlatformMove, null, 2),
        Def("partner", AchievementKind.SponsorInstallments, 24, 2),
        Def("survivor", AchievementKind.DramaSurvived, 90, 2),
    ];

    private static CareerState Fresh() => M3BPlay.Fresh();

    private static ImmutableHashSet<string> Earned(CareerState s, bool completed = false) => Achievements.Evaluate(s, completed, Defs);

    [Fact]
    public void Each_kind_is_earned_at_its_threshold()
    {
        var s = Fresh();
        Assert.Empty(Earned(s));
        Assert.Contains("fans", Earned(s with { Followers = 10_000 }));
        Assert.DoesNotContain("fans", Earned(s with { Followers = 9_999 }));
        Assert.Contains("rich", Earned(s with { CashPln = 1_000_000 }));
        Assert.Contains("done", Earned(s, completed: true));
        Assert.Contains("master", Earned(s with { Skill = s.Skill.With(Category.Chess, Fixed.FromInt(90)) }));
        Assert.Contains("one-category", Earned(s with { Metrics = s.Metrics with { WeeksPerCategory = s.Metrics.WeeksPerCategory.With(Category.Fps, 520) } }));
        Assert.Contains("moved", Earned(s with { PlatformSinceWeek = 30 }));
        Assert.Contains("partner", Earned(s with { Metrics = s.Metrics with { SponsorInstallments = 24 } }));
        var dramatic = s with { Metrics = s.Metrics with { PeakDrama = Fixed.FromInt(90) } };
        Assert.DoesNotContain("survivor", Earned(dramatic));
        Assert.Contains("survivor", Earned(dramatic, completed: true));
    }

    [Fact]
    public void Achievements_are_never_revoked_and_points_come_from_the_definitions()
    {
        var s = Fresh() with { Achievements = ["fans", "moved"] };
        Assert.Equal(["fans", "moved"], Earned(s).Order(StringComparer.Ordinal));
        Assert.Equal(3, Achievements.Points(s.Achievements, Defs));
    }

    [Fact]
    public void Without_definitions_the_m1_set_applies()
    {
        var s = Fresh() with { Followers = 10_000 };
        Assert.Equal(Achievements.Evaluate(s, completed: false), Achievements.Evaluate(s, false, null));
        Assert.Equal(Achievements.Points(["followers-10k", "career-completed"]), Achievements.Points(["followers-10k", "career-completed"], null));
    }

    [Fact]
    public void Settlement_tracks_peak_drama_and_paid_installments_only_with_the_section()
    {
        var c = M3BPlay.C with { Balance = M3BPlay.C.Balance with { Achievements = Defs } };
        var s = M3BPlay.Fresh() with { Drama = Fixed.FromInt(55) };
        var started = Start(s, c);
        var (after, _) = M3BPlay.Engine.SettleWeek(started, c, started.StateVersion).Value;
        Assert.True(after.Metrics.PeakDrama!.Value >= Fixed.FromInt(50));
        Assert.Equal(0, after.Metrics.SponsorInstallments);

        var (plain, _) = M3BPlay.Week(s, M3BPlay.Plan(MainAction.NoAction));
        Assert.Null(plain.Metrics.PeakDrama);
        Assert.Null(plain.Metrics.SponsorInstallments);
    }

    [Fact]
    public void A_paid_installment_counts()
    {
        var c = M3BPlay.C with { Balance = M3BPlay.C.Balance with { Achievements = Defs } };
        var s = M3BPlay.Fresh() with
        {
            Week = new CareerWeek(16),
            LastSettledWeek = 15,
            Energy = Fixed.FromInt(60),
            Contracts = [new SponsorContract("trusted-tea:4", "trusted-tea", SponsorKind.Contract, 4, 16, 27, 400, 0)],
        };
        var started = M3BPlay.Engine.StartWeek(s, c, M3BPlay.Plan(MainAction.PitchFulfillSponsor), s.StateVersion).Value;
        var resolved = Resolve(started, c);
        var (after, _) = M3BPlay.Engine.SettleWeek(resolved, c, resolved.StateVersion).Value;
        Assert.Equal(1, after.Metrics.SponsorInstallments);
    }

    [Fact]
    public void Retirement_scores_the_definitions()
    {
        var c = M3BPlay.C with { Balance = M3BPlay.C.Balance with { Achievements = Defs } };
        var s = M3BPlay.Fresh() with { Followers = 20_000, PlatformSinceWeek = 30 };
        var retired = M3BPlay.Engine.Retire(s, c, s.StateVersion).Value;
        Assert.Equal(["fans", "moved"], retired.Achievements.Order(StringComparer.Ordinal));
        Assert.Equal(Fixed.FromRatio(3, 10), retired.FinalScore!.A);
    }

    private static CareerState Start(CareerState s, ContentSnapshot c)
        => Resolve(M3BPlay.Engine.StartWeek(s, c, M3BPlay.Plan(MainAction.NoAction, Category.JustChatting), s.StateVersion).Value, c);

    private static CareerState Resolve(CareerState s, ContentSnapshot c)
    {
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            s = M3BPlay.Engine.ResolveEvent(s, c, M3BPlay.Engine.PresentEvent(s, c).Value.Choices[^1].ChoiceId, s.StateVersion).Value;
        }

        return s;
    }
}
