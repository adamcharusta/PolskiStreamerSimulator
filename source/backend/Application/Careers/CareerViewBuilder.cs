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
        new EquipmentView(
            s.Equipment[EquipmentSlot.Microphone],
            s.Equipment[EquipmentSlot.Camera],
            s.Equipment[EquipmentSlot.Computer],
            s.Equipment[EquipmentSlot.Connection]));

    private static CategoryValuesView Categories(CategoryValues<Fixed> v) => new(
        Decimals.Of(v[Category.JustChatting]),
        Decimals.Of(v[Category.Fps]),
        Decimals.Of(v[Category.Moba]),
        Decimals.Of(v[Category.Chess]),
        Decimals.Of(v[Category.Gambling]));

    private NextStepView Next(CareerState s, ContentSnapshot c)
    {
        if (s.Status == CareerStatus.Ended)
        {
            return new NextStepView(NextStepKind.Ended, [], null);
        }

        if (s.Pending is null)
        {
            var plans = Expect(engine.LegalPlans(s, c)).Select(l => new PlanOptionView(ReportViews.Plan(l.Plan), l.DisabledReason));
            return new NextStepView(NextStepKind.PlanWeek, [.. plans], null);
        }

        return s.Pending.Resolved.Length < s.Pending.EventIds.Length
            ? new NextStepView(NextStepKind.ResolveEvent, [], Event(Expect(engine.PresentEvent(s, c)), c))
            : new NextStepView(NextStepKind.SettleWeek, [], null);
    }

    private static EventView Event(EventPresentation presented, ContentSnapshot c)
    {
        var definition = c.Events.First(e => string.Equals(e.Id, presented.EventId, StringComparison.Ordinal));
        return new EventView(presented.EventId, definition.TitleKey, definition.TextPl, [.. presented.Choices.Select(o => Choice(o, definition))]);
    }

    private static ChoiceView Choice(ChoiceOdds odds, EventDefinition definition)
    {
        var choice = definition.Choices.First(x => string.Equals(x.Id, odds.ChoiceId, StringComparison.Ordinal));
        return new ChoiceView(
            odds.ChoiceId,
            choice.LabelKey,
            choice.LabelPl,
            odds.Guaranteed,
            Decimals.Of(odds.Probability),
            odds.Threshold,
            [.. odds.Modifiers.Select(ReportViews.Modifier)],
            odds.AllowsCritical);
    }

    private static T Expect<T>(Result<T> result)
        => result.IsSuccess ? result.Value : throw new InvalidOperationException($"Career view failed: {result.Error.Code} ({result.Error.Detail}).");
}
