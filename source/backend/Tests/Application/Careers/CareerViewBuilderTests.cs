using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class CareerViewBuilderTests
{
    private static readonly ContentSnapshot Content = TestWorld.Content;
    private readonly CareerViewBuilder views = new(TestWorld.Engine);

    private static CareerState Fresh() => TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, 1);

    private static CareerState Started(CareerState s)
        => TestWorld.Engine.StartWeek(s, Content, new WeekPlan(MainAction.Rest, Category.Fps), s.StateVersion).Value;

    private static CareerState AllResolved(CareerState s)
    {
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            var presented = TestWorld.Engine.PresentEvent(s, Content).Value;
            s = TestWorld.Engine.ResolveEvent(s, Content, presented.Choices[0].ChoiceId, s.StateVersion).Value;
        }

        return s;
    }

    [Fact]
    public void A_new_week_offers_every_plan_with_its_disabled_reason()
    {
        var s = Fresh();

        var view = views.Build(Guid.NewGuid(), "x", s, Content, null);

        Assert.Equal(NextStepKind.PlanWeek, view.Next.Kind);
        Assert.Equal(TestWorld.Engine.LegalPlans(s, Content).Value.Count, view.Next.Plans.Count);
        Assert.Contains(view.Next.Plans, p => p.DisabledReason is null);
        Assert.Null(view.Next.Event);
    }

    [Fact]
    public void A_started_week_shows_the_next_event_with_polish_text_and_odds()
    {
        var view = views.Build(Guid.NewGuid(), "x", Started(Fresh()), Content, null);

        Assert.Equal(NextStepKind.ResolveEvent, view.Next.Kind);
        var shown = view.Next.Event!;
        var definition = Content.Events.Single(e => e.Id == shown.EventId);
        Assert.Equal((definition.TitleKey, (string?)null, definition.TextPl, false), (shown.TitleKey, shown.Title, shown.Text, shown.Mandatory));
        Assert.Equal(definition.Choices.Select(c => c.LabelPl), shown.Choices.Select(c => c.Label));
        Assert.All(shown.Choices, c => Assert.Null(c.UnavailableReason));
        Assert.All(shown.Choices, c => Assert.InRange(c.Probability, 0m, 1m));
        Assert.Empty(view.Next.Plans);
    }

    [Fact]
    public void A_week_with_every_event_resolved_asks_to_settle()
        => Assert.Equal(NextStepKind.SettleWeek, views.Build(Guid.NewGuid(), "x", AllResolved(Started(Fresh())), Content, null).Next.Kind);

    [Fact]
    public void An_ended_career_has_no_next_step()
    {
        var ended = Fresh() with { Status = CareerStatus.Ended, Ending = EndingReason.Insolvency };

        var view = views.Build(Guid.NewGuid(), "x", ended, Content, null);

        Assert.Equal(NextStepKind.Ended, view.Next.Kind);
        Assert.Equal(EndingReason.Insolvency, view.Ending);
    }

    [Fact]
    public void Fixed_values_convert_exactly()
    {
        Assert.Equal(0.333333m, Decimals.Of(Fixed.FromRatio(1, 3)));
        Assert.Equal(-0.000001m, Decimals.Of(new Fixed(-1)));
        Assert.Equal(100m, Decimals.Of(Fixed.Hundred));
    }
}
