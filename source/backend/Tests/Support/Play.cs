using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Plays whole weeks through the engine: start, resolve each event with its first choice, settle.</summary>
public static class Play
{
    /// <summary>Rest when energy is below 30, otherwise stream with no main action; primary category FPS.</summary>
    public static WeekPlan DefaultPlan(CareerState s)
        => new(s.Energy < Fixed.FromInt(30) || s.Burnout == Fixed.Hundred ? MainAction.Rest : MainAction.NoAction, Category.Fps);

    public static (CareerState State, WeekReport Report) OneWeek(CareerState s, WeekPlan? plan = null, ContentSnapshot? content = null)
    {
        var c = content ?? TestWorld.Content;
        var engine = TestWorld.Engine;
        s = engine.StartWeek(s, c, plan ?? DefaultPlan(s), s.StateVersion).Value;
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            var presentation = engine.PresentEvent(s, c).Value;
            s = engine.ResolveEvent(s, c, presentation.Choices[0].ChoiceId, s.StateVersion).Value;
        }

        return engine.SettleWeek(s, c, s.StateVersion).Value;
    }

    public static CareerState Weeks(CareerState s, int weeks)
    {
        for (var i = 0; i < weeks; i++)
        {
            s = OneWeek(s).State;
        }

        return s;
    }
}
