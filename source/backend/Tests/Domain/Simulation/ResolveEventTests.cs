using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class ResolveEventTests
{
    private static readonly WeekEngine Engine = TestWorld.Engine;
    private static readonly ContentSnapshot Content = TestWorld.Content;
    private static readonly Fixed Base55 = Fixed.FromRatio(55, 100);

    private static CareerState Started(ulong seed = 1)
        => Engine.StartWeek(TestCareers.Dorm(seed), Content, new WeekPlan(MainAction.Rest, Category.Fps), 0).Value;

    private static RollResult ResolveFirst(int roll, string choiceId, Fixed? baseChance = null)
    {
        var (s, content) = TestScenarios.PendingWithFirstRoll(roll, baseChance ?? Base55);
        return Engine.ResolveEvent(s, content, choiceId, s.StateVersion).Value.Pending!.Resolved[0];
    }

    [Fact]
    public void Presentation_shows_odds_for_the_next_event_and_draws_no_random()
    {
        var s = Started();
        var first = Engine.PresentEvent(s, Content).Value;
        Assert.Equal(s.Pending!.EventIds[0], first.EventId);
        Assert.Equivalent(first, Engine.PresentEvent(s, Content).Value, strict: true);
        var definition = Content.Events.Single(e => e.Id == first.EventId);
        Assert.Equal(definition.Choices.Select(c => c.Id), first.Choices.Select(c => c.ChoiceId));
        Assert.Equivalent(definition.Choices.Select(c => OddsCalculator.For(s, c, Content.Balance)).ToArray(), first.Choices.ToArray(), strict: true);
    }

    [Fact]
    public void Presentation_moves_to_the_next_unresolved_event()
    {
        var s = Started();
        var first = Engine.PresentEvent(s, Content).Value;
        var s2 = Engine.ResolveEvent(s, Content, first.Choices[^1].ChoiceId, s.StateVersion).Value;
        Assert.Equal(s.Pending!.EventIds[1], Engine.PresentEvent(s2, Content).Value.EventId);
    }

    [Fact]
    public void Roll_equal_threshold_succeeds()
    {
        var result = ResolveFirst(55, "risky");
        Assert.Equal(55, result.Roll);
        Assert.Equal(55, result.Threshold);
        Assert.Equal(RollOutcome.Success, result.Outcome);
    }

    [Fact]
    public void Roll_above_threshold_fails() => Assert.Equal(RollOutcome.Failure, ResolveFirst(56, "risky").Outcome);

    [Fact]
    public void Roll_100_is_critical_failure_only_when_allowed()
    {
        Assert.Equal(RollOutcome.CriticalFailure, ResolveFirst(100, "risky-crit").Outcome);
        Assert.Equal(RollOutcome.Failure, ResolveFirst(100, "risky").Outcome);
    }

    [Fact]
    public void Roll_1_is_critical_success_only_when_allowed()
    {
        Assert.Equal(RollOutcome.CriticalSuccess, ResolveFirst(1, "risky-crit").Outcome);
        Assert.Equal(RollOutcome.Success, ResolveFirst(1, "risky").Outcome);
    }

    [Fact]
    public void Critical_success_applies_its_own_effects()
    {
        var (s, content) = TestScenarios.PendingWithFirstRoll(1, Base55);
        var p = Engine.ResolveEvent(s, content, "risky-crit", s.StateVersion).Value.Pending!;
        Assert.Equal(Fixed.FromInt(8), p.EventQuality);
        Assert.Equal(Fixed.One, p.AudienceMultiplier);
    }

    [Fact]
    public void Success_effects_accumulate_in_the_pending_week()
    {
        var (s, content) = TestScenarios.PendingWithFirstRoll(10, Base55);
        var s1 = Engine.ResolveEvent(s, content, "risky", s.StateVersion).Value;
        var p = s1.Pending!;
        Assert.Equal(s.Credibility + Fixed.FromInt(2), s1.Credibility);
        Assert.Equal(100, p.CashDeltaFromEvents);
        Assert.Equal(Fixed.FromRatio(12, 10), p.AudienceMultiplier);
        Assert.Equal(Fixed.FromInt(5), p.EventQuality);
        Assert.Contains(new ReportLine("income:event", AmountPln: 100, Detail: TestScenarios.ProbeEventId), p.Lines);
        Assert.Contains(new ReportLine("stat:Credibility", Delta: Fixed.FromInt(2), Detail: TestScenarios.ProbeEventId), p.Lines);
        Assert.Equal(s.CashPln, s1.CashPln);
    }

    [Fact]
    public void Failure_effects_record_costs_and_lower_the_multiplier()
    {
        var (s, content) = TestScenarios.PendingWithFirstRoll(90, Base55);
        var s1 = Engine.ResolveEvent(s, content, "risky", s.StateVersion).Value;
        var p = s1.Pending!;
        Assert.Equal(s.Drama + Fixed.FromInt(3), s1.Drama);
        Assert.Equal(-50, p.CashDeltaFromEvents);
        Assert.Equal(Fixed.FromRatio(9, 10), p.AudienceMultiplier);
        Assert.Equal(Fixed.FromInt(-4), p.EventQuality);
        Assert.Contains(new ReportLine("cost:event", AmountPln: 50, Detail: TestScenarios.ProbeEventId), p.Lines);
    }

    [Fact]
    public void Resolution_records_roll_threshold_and_modifiers()
    {
        var s = Enumerable.Range(1, 100).Select(seed => Started((ulong)seed)).First(x => x.Pending!.EventIds.Contains("chat-raid"));
        var raidIndex = s.Pending!.EventIds.IndexOf("chat-raid");
        for (var i = 0; i < raidIndex; i++)
        {
            var presented = Engine.PresentEvent(s, Content).Value;
            s = Engine.ResolveEvent(s, Content, presented.Choices[^1].ChoiceId, s.StateVersion).Value;
        }

        var resolved = Engine.ResolveEvent(s, Content, "welcome", s.StateVersion).Value.Pending!.Resolved[raidIndex];
        // base 0.55 + credibility 50 × 0.002
        Assert.Equal("chat-raid", resolved.EventId);
        Assert.Equal("welcome", resolved.ChoiceId);
        Assert.Equal(65, resolved.Threshold);
        Assert.Equal([new ModifierLine("modifier.credibility", Fixed.FromRatio(1, 10))], resolved.Modifiers);
        Assert.NotNull(resolved.Roll);
        Assert.Equal(resolved.Roll <= 65 ? RollOutcome.Success : RollOutcome.Failure, resolved.Outcome);
    }

    [Fact]
    public void Guaranteed_choice_draws_nothing_and_applies_success()
    {
        var (s, content) = TestScenarios.PendingWithFirstRoll(70, Base55);
        var s1 = Engine.ResolveEvent(s, content, "safe", s.StateVersion).Value;
        var result = s1.Pending!.Resolved[0];
        Assert.Equal(s.Rng, s1.Rng);
        Assert.Null(result.Roll);
        Assert.Equal(RollOutcome.Guaranteed, result.Outcome);
        Assert.Equal(100, s1.Pending.CashDeltaFromEvents);
    }

    [Fact]
    public void Resolution_sets_cooldown_counts_metrics_and_bumps_version()
    {
        var (s, content) = TestScenarios.PendingWithFirstRoll(100, Base55);
        var s1 = Engine.ResolveEvent(s, content, "risky-crit", s.StateVersion).Value;
        Assert.Equal(1 + TestScenarios.ProbeCooldownWeeks, s1.EventCooldowns[TestScenarios.ProbeEventId]);
        Assert.Equal(1, s1.Metrics.EventsResolved);
        Assert.Equal(1, s1.Metrics.Criticals);
        Assert.Equal(s.StateVersion + 1, s1.StateVersion);
        Assert.NotEqual(s.Rng, s1.Rng);
    }

    [Fact]
    public void Resolving_with_stale_version_draws_nothing()
    {
        var s = Started();
        var choice = Engine.PresentEvent(s, Content).Value.Choices[0].ChoiceId;
        Assert.Equal(DomainErrorCode.StaleVersion, Engine.ResolveEvent(s, Content, choice, s.StateVersion - 1).Error.Code);
    }

    [Fact]
    public void Unknown_choice_is_rejected()
        => Assert.Equal(new DomainError(DomainErrorCode.UnknownChoice, "nope"), Engine.ResolveEvent(Started(), Content, "nope", 1).Error);

    [Fact]
    public void Events_cannot_be_resolved_beyond_the_selection()
    {
        var (s, content) = TestScenarios.PendingWithFirstRoll(70, Base55);
        var s1 = Engine.ResolveEvent(s, content, "safe", s.StateVersion).Value;
        Assert.Equal(new DomainError(DomainErrorCode.IllegalAction, "all-resolved"), Engine.PresentEvent(s1, content).Error);
        Assert.Equal(new DomainError(DomainErrorCode.IllegalAction, "all-resolved"), Engine.ResolveEvent(s1, content, "safe", s1.StateVersion).Error);
    }

    [Fact]
    public void Presenting_without_a_started_week_is_rejected()
    {
        Assert.Equal(DomainErrorCode.NoPendingWeek, Engine.PresentEvent(TestCareers.Dorm(1), Content).Error.Code);
        Assert.Equal(DomainErrorCode.NoPendingWeek, Engine.ResolveEvent(TestCareers.Dorm(1), Content, "welcome", 0).Error.Code);
    }
}
