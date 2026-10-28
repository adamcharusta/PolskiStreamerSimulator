using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public class M3FollowUpTests
{
    private static WeekEngine Engine => M3Play.Engine;

    private static CareerState Fresh(ContentSnapshot c, ulong seed = 3)
        => CareerFactory.Create(M3Ruleset.SystemsV1, c.Digest, c.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, seed);

    private static EventDefinition Live(string id, int fromWeek = 1, ImmutableArray<Effect> effects = default)
    {
        var e = M3TestContent.Decision(id, EventPhase.Live, fromWeek, CareerWeek.Last, oneTime: false);
        return effects.IsDefault
            ? e
            : e with { Choices = [e.Choices[0] with { Outcomes = new OutcomeSet(effects, [], null, null) }] };
    }

    /// <summary>The test content with only the given live events, the crises and no random decisions.</summary>
    private static ContentSnapshot Content(string digest, params EventDefinition[] live)
    {
        var m3 = M3TestContent.Valid();
        return m3 with
        {
            Digest = digest,
            Balance = m3.Balance with { DecisionChancePerWeek = Fixed.Zero },
            Events = [.. live, .. m3.Events.Where(e => e.Id is "test-crisis" or "burnout-recovery")],
        };
    }

    private static (CareerState Started, CareerState Settled) PlayWeek(CareerState s, ContentSnapshot c)
    {
        s = Engine.StartWeek(s, c, new WeekPlan(MainAction.NoAction, Category.Fps), s.StateVersion).Value;
        var started = s;
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            s = Engine.ResolveEvent(s, c, Engine.PresentEvent(s, c).Value.Choices[0].ChoiceId, s.StateVersion).Value;
        }

        return (started, Engine.SettleWeek(s, c, s.StateVersion).Value.State);
    }

    [Fact]
    public void Decision_follow_up_opens_before_its_due_week_without_a_chance_roll()
    {
        var c = Content("m3-follow-decision", Live("trigger", effects: [new Effect(EffectKind.FollowUp, null, Fixed.Zero, Target: "test-crisis", Weeks: 2)]));
        var s = Engine.StartWeek(Fresh(c), c, new WeekPlan(MainAction.NoAction, Category.Fps), 0).Value;
        Assert.Equal(["trigger"], s.Pending!.EventIds);
        s = Engine.ResolveEvent(s, c, "calm", s.StateVersion).Value;
        Assert.Equal([new ScheduledEvent("test-crisis", 3)], s.Scheduled!.Value);

        s = Engine.SettleWeek(s, c, s.StateVersion).Value.State;
        Assert.Empty(s.PendingDecisions!.Value);
        s = PlayWeek(s, c).Settled;

        Assert.Equal(3, s.Week.Value);
        Assert.Equal(["test-crisis"], s.PendingDecisions!.Value);
        Assert.Empty(s.Scheduled!.Value);
    }

    [Fact]
    public void Repeated_decision_follow_ups_open_once_and_missing_events_are_dropped()
    {
        var c = Content("m3-follow-repeated", Live("filler-a"));
        var s = Fresh(c) with
        {
            Scheduled = [new ScheduledEvent("test-crisis", 2), new ScheduledEvent("gone", 2), new ScheduledEvent("test-crisis", 2)],
        };

        s = PlayWeek(s, c).Settled;

        Assert.Equal(["test-crisis"], s.PendingDecisions!.Value);
        Assert.Empty(s.Scheduled!.Value);
    }

    [Fact]
    public void Live_follow_up_takes_the_first_event_slot_of_its_due_week()
    {
        var c = Content("m3-follow-live", Live("echo", fromWeek: 500), Live("filler-a"), Live("filler-b"), Live("filler-c"));
        var s = Fresh(c) with { Scheduled = [new ScheduledEvent("echo", 1)] };

        var (started, _) = PlayWeek(s, c);

        Assert.Equal("echo", started.Pending!.EventIds[0]);
        Assert.InRange(started.Pending.EventIds.Length, 2, 3);
        Assert.Empty(started.Scheduled!.Value);
    }

    [Fact]
    public void Duplicate_live_follow_ups_fire_once()
    {
        var c = Content("m3-follow-duplicate", Live("echo", fromWeek: 500), Live("filler-a"), Live("filler-b"), Live("filler-c"));
        var s = Fresh(c) with { Scheduled = [new ScheduledEvent("echo", 1), new ScheduledEvent("echo", 1)] };

        var (started, _) = PlayWeek(s, c);

        Assert.Single(started.Pending!.EventIds, "echo");
        Assert.Empty(started.Scheduled!.Value);
    }

    [Fact]
    public void Live_follow_up_waits_while_its_event_cools_down()
    {
        var c = Content("m3-follow-cooling", Live("echo"), Live("filler-a"), Live("filler-b"));
        var s = Fresh(c) with
        {
            Scheduled = [new ScheduledEvent("echo", 1)],
            EventCooldowns = ImmutableDictionary<string, int>.Empty.Add("echo", 2),
        };

        var (started, settled) = PlayWeek(s, c);

        Assert.DoesNotContain("echo", started.Pending!.EventIds);
        Assert.Equal([new ScheduledEvent("echo", 1)], started.Scheduled!.Value);
        Assert.Equal("echo", PlayWeek(settled, c).Started.Pending!.EventIds[0]);
    }
}
