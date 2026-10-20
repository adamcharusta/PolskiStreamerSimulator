using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>
/// Builds the career view from the state; it queries the career's own ruleset for plans and odds, so it is written
/// out instead of being mapped.
/// </summary>
public sealed class CareerViewBuilder(WeekEngine engine)
{
    public CareerView Build(Guid careerId, string nickname, CareerState s, ContentSnapshot c, WeekReportView? lastReport)
        => new(
            careerId,
            nickname,
            s.RulesetId,
            s.StateVersion,
            s.Status,
            s.Ending,
            s.Background,
            s.Difficulty,
            s.Pronouns,
            s.Week.Value,
            s.LastSettledWeek,
            Stats(s),
            Next(s, c),
            lastReport);

    public static CareerStatsView Stats(CareerState s) => new(
        s.CashPln,
        s.Followers,
        s.LastLiveViewers,
        Decimals.Of(s.Drama),
        Decimals.Of(s.Credibility),
        Decimals.Of(s.Energy),
        Decimals.Of(s.Burnout),
        Categories(s.Skill),
        Categories(s.Boredom),
        Equipment(s.Equipment));

    public static CategoryValuesView Categories(CategoryValues<Fixed> v) => new(
        Decimals.Of(v[Category.JustChatting]),
        Decimals.Of(v[Category.Fps]),
        Decimals.Of(v[Category.Moba]),
        Decimals.Of(v[Category.Chess]),
        Decimals.Of(v[Category.Gambling]));

    public static EquipmentView Equipment(IReadOnlyDictionary<EquipmentSlot, int> tiers) => new(
        tiers[EquipmentSlot.Microphone],
        tiers[EquipmentSlot.Camera],
        tiers[EquipmentSlot.Computer],
        tiers[EquipmentSlot.Connection]);

    private NextStepView Next(CareerState s, ContentSnapshot c)
    {
        if (s.Status == CareerStatus.Ended)
        {
            return new NextStepView(NextStepKind.Ended, [], null);
        }

        if (s.Pending is null)
        {
            return Expect(engine.RequiresPlayerDecision(s, c)) && Decision(s, c) is { } decision
                ? new NextStepView(NextStepKind.ResolveDecision, [], Event(decision, c, mandatory: true))
                : new NextStepView(NextStepKind.PlanWeek, Plans(s, c), null);
        }

        return s.Pending.Resolved.Length < s.Pending.EventIds.Length
            ? new NextStepView(NextStepKind.ResolveEvent, [], Event(Expect(engine.PresentEvent(s, c)), c, mandatory: false))
            : new NextStepView(NextStepKind.SettleWeek, [], null);
    }

    /// <summary>
    /// The queued decision, or <c>null</c> for a pause that queues no decision event (the week is then planned by hand);
    /// any other failure, such as a queued event missing from the content, is a server fault rather than a dead end.
    /// </summary>
    private EventPresentation? Decision(CareerState s, ContentSnapshot c)
    {
        var decision = engine.PresentDecision(s, c);
        return !decision.IsSuccess && decision.Error.Code == DomainErrorCode.NoPendingDecision ? null : Expect(decision);
    }

    /// <summary>Every plan with its disabled reason and, for a rolled action (networking), the odds of its roll.</summary>
    private PlanOptionView[] Plans(CareerState s, ContentSnapshot c)
        => [.. Expect(engine.LegalPlans(s, c)).Select(l => new PlanOptionView(
            ReportViews.Plan(l.Plan),
            l.DisabledReason,
            Expect(engine.PresentAction(s, c, l.Plan)) is { } odds ? Choice(odds, c.Balance.NetworkCollaborate!) : null))];

    private static EventView Event(EventPresentation presented, ContentSnapshot c, bool mandatory)
    {
        var definition = c.Events.First(e => string.Equals(e.Id, presented.EventId, StringComparison.Ordinal));
        return new EventView(
            presented.EventId,
            definition.TitleKey,
            definition.Title,
            definition.TextPl,
            [.. presented.Choices.Select(o => Choice(o, definition.Choices.First(x => string.Equals(x.Id, o.ChoiceId, StringComparison.Ordinal))))],
            mandatory);
    }

    private static ChoiceView Choice(ChoiceOdds odds, ChoiceDefinition choice)
        => new(
            odds.ChoiceId,
            choice.LabelKey,
            choice.LabelPl,
            odds.Guaranteed,
            Decimals.Of(odds.Probability),
            odds.Threshold,
            [.. odds.Modifiers.Select(ReportViews.Modifier)],
            odds.AllowsCritical,
            Decimals.Of(odds.ViralOnCritical),
            odds.UnavailableReason);

    private static T Expect<T>(Result<T> result)
        => result.IsSuccess ? result.Value : throw new InvalidOperationException($"Career view failed: {result.Error.Code} ({result.Error.Detail}).");
}
