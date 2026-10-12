using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class StartWeekTests
{
    private static readonly WeekEngine Engine = TestWorld.Engine;
    private static readonly ContentSnapshot Content = TestWorld.Content;

    private static DomainError Rejection(CareerState s, WeekPlan plan) => Engine.StartWeek(s, Content, plan, s.StateVersion).Error;

    [Fact]
    public void Rest_restores_energy_and_lowers_burnout()
    {
        var s0 = TestCareers.Dorm(1) with { Energy = Fixed.FromInt(40), Burnout = Fixed.FromInt(20) };
        var s1 = Engine.StartWeek(s0, Content, new(MainAction.Rest, Category.Fps), 0).Value;
        Assert.Equal(Fixed.FromInt(65), s1.Energy);
        Assert.Equal(Fixed.FromInt(8), s1.Burnout);
        Assert.InRange(s1.Pending!.EventIds.Length, 2, 3);
        Assert.Equal(1, s1.StateVersion);
    }

    [Fact]
    public void Rest_is_clamped_to_the_stat_range()
    {
        var s1 = Engine.StartWeek(TestCareers.Dorm(1) with { Energy = Fixed.FromInt(90) }, Content, new(MainAction.Rest, Category.Fps), 0).Value;
        Assert.Equal(Fixed.Hundred, s1.Energy);
        Assert.Equal(Fixed.Zero, s1.Burnout);
    }

    [Fact]
    public void No_action_recovers_a_little_energy()
        => Assert.Equal(Fixed.FromInt(75), Engine.StartWeek(TestCareers.Dorm(1), Content, new(MainAction.NoAction, Category.Fps), 0).Value.Energy);

    [Fact]
    public void Training_raises_the_target_skill_and_costs_energy()
    {
        var s1 = Engine.StartWeek(TestCareers.Dorm(1), Content, new(MainAction.TrainCategory, Category.Fps, TrainTarget: Category.Chess), 0).Value;
        // gain = NextInt(2, 4) × (100 − 10)/100, so 1.8 to 3.6 points
        Assert.InRange(s1.Skill[Category.Chess].Raw, 11_800_000, 13_600_000);
        Assert.Equal(Fixed.FromInt(18), s1.Skill[Category.Fps]);
        Assert.Equal(Fixed.FromInt(62), s1.Energy);
    }

    [Fact]
    public void Training_without_a_target_trains_the_primary_category()
    {
        var s1 = Engine.StartWeek(TestCareers.Dorm(1), Content, new(MainAction.TrainCategory, Category.Moba), 0).Value;
        Assert.True(s1.Skill[Category.Moba] > Fixed.FromInt(10));
    }

    [Fact]
    public void Training_at_high_skill_still_gains_at_least_a_fifth()
    {
        var s0 = TestCareers.Dorm(1);
        s0 = s0 with { Skill = s0.Skill.With(Category.Fps, Fixed.FromInt(95)) };
        var s1 = Engine.StartWeek(s0, Content, new(MainAction.TrainCategory, Category.Fps), 0).Value;
        // gain = NextInt(2, 4) × max(0.2, 0.05) = 0.4 to 0.8 points
        Assert.InRange(s1.Skill[Category.Fps].Raw, 95_400_000, 95_800_000);
    }

    [Fact]
    public void Managing_the_community_trades_drama_and_boredom_for_credibility()
    {
        var s0 = TestCareers.Dorm(1);
        s0 = s0 with { Drama = Fixed.FromInt(10), Boredom = s0.Boredom.With(Category.Fps, Fixed.FromInt(20)) };
        var s1 = Engine.StartWeek(s0, Content, new(MainAction.ManageCommunity, Category.Fps), 0).Value;
        Assert.Equal(Fixed.FromInt(53), s1.Credibility);
        Assert.Equal(Fixed.FromInt(6), s1.Drama);
        Assert.Equal(Fixed.FromInt(15), s1.Boredom[Category.Fps]);
    }

    [Fact]
    public void Improving_production_buys_the_next_tier_and_records_the_cost()
    {
        var s1 = Engine.StartWeek(TestCareers.Dorm(1), Content, new(MainAction.ImproveProduction, Category.Fps, UpgradeSlot: EquipmentSlot.Camera), 0).Value;
        Assert.Equal(1, s1.Equipment[EquipmentSlot.Camera]);
        Assert.Equal(1_400, s1.CashPln);
        Assert.Contains(new ReportLine("cost:equipment", AmountPln: 600, Detail: "Camera"), s1.Pending!.Lines);
    }

    [Fact]
    public void Unaffordable_upgrade_is_illegal_and_state_unchanged()
    {
        var s0 = TestCareers.Dorm(1) with { CashPln = 100 };
        var r = Engine.StartWeek(s0, Content, new(MainAction.ImproveProduction, Category.Fps, UpgradeSlot: EquipmentSlot.Camera), 0);
        Assert.Equal(DomainErrorCode.IllegalAction, r.Error.Code);
        Assert.Equal("unaffordable", r.Error.Detail);
    }

    [Fact]
    public void Negative_cash_blocks_spend()
        => Assert.Equal(
            new DomainError(DomainErrorCode.IllegalAction, "negative-cash"),
            Rejection(TestCareers.Dorm(1) with { CashPln = -1 }, new(MainAction.ImproveProduction, Category.Fps, UpgradeSlot: EquipmentSlot.Camera)));

    [Fact]
    public void Top_tier_cannot_be_upgraded()
    {
        var s0 = TestCareers.Dorm(1);
        s0 = s0 with { CashPln = 100_000, Equipment = s0.Equipment.SetItem(EquipmentSlot.Camera, 4) };
        Assert.Equal(new DomainError(DomainErrorCode.IllegalAction, "max-tier"), Rejection(s0, new(MainAction.ImproveProduction, Category.Fps, UpgradeSlot: EquipmentSlot.Camera)));
    }

    [Fact]
    public void Upgrade_needs_a_slot()
        => Assert.Equal(new DomainError(DomainErrorCode.IllegalAction, "missing-slot"), Rejection(TestCareers.Dorm(1), new(MainAction.ImproveProduction, Category.Fps)));

    [Fact]
    public void Stale_version_is_rejected()
    {
        var r = Engine.StartWeek(TestCareers.Dorm(1), Content, new(MainAction.Rest, Category.Fps), 5);
        Assert.Equal(DomainErrorCode.StaleVersion, r.Error.Code);
    }

    [Fact]
    public void Second_start_in_same_week_is_rejected()
    {
        var s1 = Engine.StartWeek(TestCareers.Dorm(1), Content, new(MainAction.Rest, Category.Fps), 0).Value;
        Assert.Equal(new DomainError(DomainErrorCode.IllegalAction, "week-in-progress"), Rejection(s1, new(MainAction.Rest, Category.Fps)));
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(50, 100)]
    public void Forced_rest_when_energy_zero_or_burnout_full(int energy, int burnout)
    {
        var s0 = TestCareers.Dorm(1) with { Energy = Fixed.FromInt(energy), Burnout = Fixed.FromInt(burnout) };
        Assert.Equal(new DomainError(DomainErrorCode.IllegalAction, "forced-rest"), Rejection(s0, new(MainAction.TrainCategory, Category.Fps)));
        Assert.True(Engine.StartWeek(s0, Content, new(MainAction.Rest, Category.Fps), 0).IsSuccess);
    }

    [Fact]
    public void Same_seed_selects_same_events()
    {
        var a = Engine.StartWeek(TestCareers.Dorm(5), Content, new(MainAction.Rest, Category.Fps), 0).Value;
        var b = Engine.StartWeek(TestCareers.Dorm(5), Content, new(MainAction.Rest, Category.Fps), 0).Value;
        Assert.Equal(a.Pending!.EventIds, b.Pending!.EventIds);
        Assert.Equal(a.Rng, b.Rng);
    }

    [Fact]
    public void Selected_events_are_distinct_known_and_not_cooling_down()
    {
        var cooling = new[] { "chat-raid", "troll-wave" };
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var s0 = TestCareers.Dorm(seed) with { Week = new CareerWeek(10) };
            s0 = s0 with { EventCooldowns = s0.EventCooldowns.Add("chat-raid", 11).Add("troll-wave", 11).Add("collab-ping", 10) };
            var ids = Engine.StartWeek(s0, Content, new(MainAction.Rest, Category.Fps), 0).Value.Pending!.EventIds;
            Assert.InRange(ids.Length, 2, 3);
            Assert.Equal(ids.Length, ids.Distinct().Count());
            Assert.All(ids, id => Assert.DoesNotContain(id, cooling));
        }
    }

    [Fact]
    public void Event_selection_follows_weights()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (ulong seed = 1; seed <= 2_000; seed++)
        {
            foreach (var id in Engine.StartWeek(TestCareers.Dorm(seed), Content, new(MainAction.Rest, Category.Fps), 0).Value.Pending!.EventIds)
            {
                counts[id] = counts.GetValueOrDefault(id) + 1;
            }
        }

        // chat-raid has weight 3 and collab-ping weight 1 out of 10
        Assert.True(counts["chat-raid"] > counts["collab-ping"] * 2, string.Join(", ", counts));
    }

    [Fact]
    public void Pending_week_starts_with_neutral_event_effects()
    {
        var p = Engine.StartWeek(TestCareers.Dorm(1), Content, new(MainAction.Rest, Category.Chess), 0).Value.Pending!;
        Assert.Equal(new CareerWeek(1), p.Week);
        Assert.Equal(new WeekPlan(MainAction.Rest, Category.Chess), p.Plan);
        Assert.Empty(p.Resolved);
        Assert.Equal(Fixed.Zero, p.EventQuality);
        Assert.Equal(Fixed.One, p.AudienceMultiplier);
        Assert.Equal(0, p.CashDeltaFromEvents);
    }

    [Fact]
    public void Started_week_roundtrips_through_canonical_json()
    {
        var s1 = Engine.StartWeek(TestCareers.Dorm(3), Content, new(MainAction.ImproveProduction, Category.Fps, UpgradeSlot: EquipmentSlot.Microphone), 0).Value;
        var bytes = CanonicalJson.Serialize(s1);
        Assert.Equal(bytes, CanonicalJson.Serialize(CanonicalJson.Deserialize(bytes)));
        Assert.Equal(s1.Pending!.EventIds, CanonicalJson.Deserialize(bytes).Pending!.EventIds);
    }

    [Fact]
    public void LegalPlans_lists_disabled_spend_with_reason()
    {
        var legal = Engine.LegalPlans(TestCareers.Dorm(1) with { CashPln = 0 }, Content).Value;
        var upgrades = legal.Where(l => l.Plan.Action == MainAction.ImproveProduction).ToArray();
        Assert.Equal(Enum.GetValues<Category>().Length * Enum.GetValues<EquipmentSlot>().Length, upgrades.Length);
        Assert.All(upgrades, u => Assert.Equal("unaffordable", u.DisabledReason));
        Assert.Contains(legal, l => l.Plan == new WeekPlan(MainAction.Rest, Category.Chess) && l.DisabledReason is null);
    }

    [Fact]
    public void LegalPlans_under_forced_rest_enable_only_rest()
    {
        var legal = Engine.LegalPlans(TestCareers.Dorm(1) with { Energy = Fixed.Zero }, Content).Value;
        Assert.All(legal, l => Assert.Equal(l.Plan.Action == MainAction.Rest ? null : "forced-rest", l.DisabledReason));
    }

    [Fact]
    public void Content_from_another_snapshot_is_rejected()
    {
        var other = Content with { Digest = new string('0', 64) };
        var r = Engine.StartWeek(TestCareers.Dorm(1), other, new(MainAction.Rest, Category.Fps), 0);
        Assert.Equal(DomainErrorCode.ContentMismatch, r.Error.Code);
    }

    [Fact]
    public void Unknown_ruleset_is_rejected_by_the_engine()
    {
        var r = Engine.StartWeek(TestCareers.Dorm(1) with { RulesetId = "future.9" }, Content, new(MainAction.Rest, Category.Fps), 0);
        Assert.Equal(new DomainError(DomainErrorCode.UnknownRuleset, "future.9"), r.Error);
    }

    [Fact]
    public void Engine_uses_the_career_ruleset_not_the_newest()
    {
        var engine = new WeekEngine(new RulesetRegistry([new LegacyRuleset(), new M1Ruleset()]));
        var legacyCareer = TestCareers.Dorm(1) with { RulesetId = LegacyRuleset.LegacyId };
        Assert.Equal(new DomainError(DomainErrorCode.IllegalAction, "legacy"), engine.StartWeek(legacyCareer, Content, new(MainAction.Rest, Category.Fps), 0).Error);
        Assert.True(engine.StartWeek(TestCareers.Dorm(1), Content, new(MainAction.Rest, Category.Fps), 0).IsSuccess);
    }
}
