using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Settlement;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class CategoryProfileTests
{
    private static readonly BalanceConstants M1 = TestWorld.Content.Balance;

    private static BalanceConstants With(Category category, Func<CategoryProfile, CategoryProfile> change)
    {
        var all = new CategoryValues<CategoryProfile>([.. Enum.GetValues<Category>().Select(M1.Profile)]);
        return M1 with { Categories = all.With(category, change(M1.Profile(category))) };
    }

    private static PendingWeek Week(Category category)
        => new(new CareerWeek(5), new WeekPlan(MainAction.NoAction, category), [], [], Fixed.Zero, Fixed.One, 0, []);

    [Fact]
    public void M1_profiles_are_neutral()
    {
        Assert.Null(M1.Categories);
        Assert.All(Enum.GetValues<Category>(), c => Assert.Equal(CategoryProfile.Neutral(M1), M1.Profile(c)));
    }

    [Fact]
    public void Reach_scales_live_viewers()
    {
        var s = TestCareers.Dorm(7) with { Followers = 10_000 };
        var rng1 = s.Rng;
        var rng2 = s.Rng;
        var neutral = AudienceRules.Compute(s, Week(Category.Chess), ref rng1, M1);
        var lower = AudienceRules.Compute(s, Week(Category.Chess), ref rng2, With(Category.Chess, p => p with { Reach = Fixed.FromRatio(3, 4) }));
        Assert.True(lower.LiveViewers < neutral.LiveViewers);
    }

    [Fact]
    public void Subscription_yield_tilts_income()
    {
        var s = TestCareers.Dorm(7);
        long Subs(BalanceConstants b) => FinanceRules.Compute(s, Week(Category.Chess), 1_000, b).Income.Single(l => l.Code == "income:subscriptions").AmountPln!.Value;
        Assert.True(Subs(With(Category.Chess, p => p with { SubYield = Fixed.FromRatio(13, 10) })) > Subs(M1));
    }

    [Fact]
    public void Mastery_lowers_boredom_gain_for_a_master()
    {
        var b = M1 with { MasteryFromSkill = Fixed.FromInt(70), MasteryBoredomRelief = 1 };
        var master = TestCareers.Dorm(7) with { Skill = CategoryValues<Fixed>.Uniform(Fixed.FromInt(80)) };
        var novice = master with { Skill = CategoryValues<Fixed>.Uniform(Fixed.FromInt(20)) };
        var rngA = master.Rng;
        var rngB = novice.Rng;
        var a = ProgressionRules.ApplySkillAndBoredom(master, Category.Fps, master.Skill, ref rngA, b);
        var n = ProgressionRules.ApplySkillAndBoredom(novice, Category.Fps, novice.Skill, ref rngB, b);
        Assert.Equal(n.Boredom[Category.Fps] - Fixed.One, a.Boredom[Category.Fps]);
    }

    [Fact]
    public void Per_stream_reputation_effects_apply_only_to_their_category()
    {
        var b = With(Category.Gambling, p => p with { DramaPerStream = Fixed.One, CredibilityPerStream = Fixed.FromRatio(-3, 10) });
        var s = TestCareers.Dorm(7);
        var after = ProgressionRules.ApplyCategoryReputation(s, Category.Gambling, b);
        Assert.Equal((s.Drama + Fixed.One, s.Credibility - Fixed.FromRatio(3, 10)), (after.Drama, after.Credibility));
        Assert.Equal(s, ProgressionRules.ApplyCategoryReputation(s, Category.Fps, b));
    }

    [Fact]
    public void M3_week_report_lists_the_category_reputation_change()
    {
        var c = M3Play.C;
        var all = new CategoryValues<CategoryProfile>([.. Enum.GetValues<Category>().Select(c.Balance.Profile)]);
        c = c with { Balance = c.Balance with { Categories = all.With(Category.Gambling, c.Balance.Profile(Category.Gambling) with { DramaPerStream = Fixed.One }) } };
        var s = M3Play.Fresh();
        var started = M3Play.Engine.StartWeek(s, c, new WeekPlan(MainAction.NoAction, Category.Gambling), s.StateVersion).Value;
        while (started.Pending!.Resolved.Length < started.Pending.EventIds.Length)
        {
            started = M3Play.Engine.ResolveEvent(started, c, M3Play.Engine.PresentEvent(started, c).Value.Choices[^1].ChoiceId, started.StateVersion).Value;
        }

        var (after, report) = M3Play.Engine.SettleWeek(started, c, started.StateVersion).Value;
        var drama = report.StatChanges.Where(l => l.Code == "stat:Drama").ToList();
        Assert.Contains(drama, l => l.Detail == "category" && l.Delta == Fixed.One);
        Assert.Equal(after.Drama - s.Drama, new Fixed(drama.Sum(l => l.Delta!.Value.Raw)));
    }
}
