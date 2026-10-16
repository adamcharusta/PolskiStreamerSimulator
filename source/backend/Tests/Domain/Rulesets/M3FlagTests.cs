using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Rulesets.M3;
using PolskiStreamerSimulator.Domain.Rulesets.Shared;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public class M3FlagTests
{
    private static readonly WeekEngine Engine = new(new RulesetRegistry([new M1Ruleset(), new M3Ruleset()]));

    private static CareerState Fresh(ContentSnapshot c, ulong seed = 3)
        => CareerFactory.Create(M3Ruleset.RulesetId, c.Digest, c.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, seed);

    private static EventDefinition Live(string id, Eligibility? eligibility = null)
    {
        var e = M3TestContent.Decision(id, EventPhase.Live, 1, CareerWeek.Last, oneTime: false);
        return e with { Eligibility = eligibility ?? e.Eligibility };
    }

    private static Eligibility Always(ImmutableArray<string> requires = default, ImmutableArray<string> forbids = default, ImmutableArray<Category> categories = default)
        => new(1, CareerWeek.Last, ImmutableDictionary<StatKey, Fixed>.Empty, ImmutableDictionary<StatKey, Fixed>.Empty, categories, requires, forbids);

    private static CareerState ResolveAll(CareerState s, ContentSnapshot c)
    {
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            s = Engine.ResolveEvent(s, c, Engine.PresentEvent(s, c).Value.Choices[^1].ChoiceId, s.StateVersion).Value;
        }

        return s;
    }

    private static CareerState Start(CareerState s, ContentSnapshot c)
        => Engine.StartWeek(s, c, new WeekPlan(MainAction.NoAction, Category.Fps), s.StateVersion).Value;

    [Fact]
    public void Flag_set_for_one_week_by_a_decision_lasts_through_the_next_week_only()
    {
        var m3 = M3TestContent.Valid();
        var milestone = m3.Events.Single(e => e.Id == "first-week");
        var setsFlag = milestone with
        {
            Choices = [milestone.Choices[0] with { Outcomes = new OutcomeSet([new Effect(EffectKind.SetFlag, null, Fixed.Zero, Target: "x", Weeks: 1)], [], null, null) }],
        };
        var c = m3 with { Digest = "m3-flag", Events = m3.Events.Replace(milestone, setsFlag) };

        var s = ResolveAll(Start(Fresh(c), c), c);
        s = Engine.SettleWeek(s, c, s.StateVersion).Value.State;
        Assert.Equal("first-week", s.PendingDecisions!.Value[0]);
        s = Engine.ResolveDecision(s, c, "calm", s.StateVersion).Value;
        Assert.Equal(2, s.Week.Value);
        Assert.True(Flags.IsActive(s, "x"));

        s = ResolveAll(Start(s, c), c);
        Assert.True(Flags.IsActive(s, "x"));

        s = Engine.SettleWeek(s, c, s.StateVersion).Value.State;
        Assert.False(Flags.IsActive(s, "x"));
        Assert.False(s.Flags!.ContainsKey("x"));
    }

    [Fact]
    public void Flag_without_weeks_lasts_until_the_last_week()
    {
        var s = Flags.Set(TestCareers.Dorm(7), "x", weeks: null);
        Assert.Equal(CareerWeek.Last, s.Flags!["x"]);
        Assert.True(Flags.IsActive(s with { Week = new CareerWeek(CareerWeek.Last) }, "x"));
        Assert.True(Flags.Expire(s, CareerWeek.Last - 1).Flags!.ContainsKey("x"));
    }

    [Fact]
    public void Clear_flag_effect_removes_the_flag()
    {
        var s = Flags.Set(TestCareers.Dorm(7), "x", weeks: 5);
        Assert.True(Flags.IsActive(s, "x"));
        s = EffectApplier.ToCarry(s, "source", [new Effect(EffectKind.ClearFlag, null, Fixed.Zero, Target: "x")]);
        Assert.False(Flags.IsActive(s, "x"));
        Assert.Equal(s, Flags.Clear(s, "x"));
    }

    [Fact]
    public void Required_and_forbidden_flags_gate_live_events()
    {
        var m3 = M3TestContent.Valid();
        var c = m3 with
        {
            Balance = m3.Balance with { EventsPerWeekMin = 1, EventsPerWeekMax = 1 },
            Events = [Live("needs-x", Always(requires: ["x"])), Live("no-x", Always(forbids: ["x"]))],
        };
        var without = TestCareers.Dorm(7);
        var flagged = Flags.Set(without, "x", weeks: 1);

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var rng = Pcg32.Create(seed, CareerFactory.CareerStream);
            Assert.Equal(["no-x"], EventSelector.Select(without, c, Category.Fps, ref rng));
            rng = Pcg32.Create(seed, CareerFactory.CareerStream);
            Assert.Equal(["needs-x"], EventSelector.Select(flagged, c, Category.Fps, ref rng));
        }
    }

    [Fact]
    public void Category_limited_live_events_are_selected_only_for_their_categories()
    {
        var m3 = M3TestContent.Valid();
        var c = m3 with
        {
            Balance = m3.Balance with { EventsPerWeekMin = 1, EventsPerWeekMax = 1 },
            Events = [Live("chess-only", Always(categories: [Category.Chess])), Live("any")],
        };
        var s = TestCareers.Dorm(7);
        var rng = Pcg32.Create(1, CareerFactory.CareerStream);
        Assert.Equal(["any"], EventSelector.Select(s, c, Category.Fps, ref rng));
        Assert.False(EventSelector.IsEligible(s, c.Events[0], Category.Fps));
        Assert.True(EventSelector.IsEligible(s, c.Events[0], Category.Chess));
        Assert.True(EventSelector.IsEligible(s, c.Events[0], category: null));
    }

    [Fact]
    public void Decisions_are_never_selected_as_live_events()
    {
        var m3 = M3TestContent.Valid();
        var c = m3 with { Events = [.. m3.Events.Where(e => e.Phase != EventPhase.Live)] };
        var rng = Pcg32.Create(1, CareerFactory.CareerStream);
        Assert.Empty(EventSelector.Select(TestCareers.Dorm(7) with { Week = new CareerWeek(2) }, c, Category.Fps, ref rng));
    }
}
