using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Plays M3-B careers (<c>m3-systems.2</c>) on <see cref="M3BTestContent"/> for economy rule tests.</summary>
public static class M3BPlay
{
    public static WeekEngine Engine => M3Play.Engine;

    public static ContentSnapshot C => M3BTestContent.Valid();

    public static CareerState Fresh(ulong seed = 3, Background background = Background.DormRoomNewcomer)
        => CareerFactory.Create(M3Ruleset.SystemsV2, C.Digest, C.Profiles[background], background, Difficulty.Normal, PronounSet.Man, seed);

    public static WeekPlan Plan(MainAction a, Category c = Category.Fps, WagerTier? wager = null, Platform? target = null)
        => new(a, c, Wager: wager, TargetPlatform: target);

    public static CareerState Start(CareerState s, WeekPlan plan)
        => Engine.StartWeek(s, C, plan, s.StateVersion).Value;

    /// <summary>Resolves every live event with its last (guaranteed in test content) choice, then settles.</summary>
    public static (CareerState State, WeekReport Report) SettleAll(CareerState s)
    {
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            s = Engine.ResolveEvent(s, C, Engine.PresentEvent(s, C).Value.Choices[^1].ChoiceId, s.StateVersion).Value;
        }

        return Engine.SettleWeek(s, C, s.StateVersion).Value;
    }

    /// <summary>Starts the week with <paramref name="plan"/> and settles it.</summary>
    public static (CareerState State, WeekReport Report) Week(CareerState s, WeekPlan plan) => SettleAll(Start(s, plan));

    /// <summary>Answers the first queued decision with <paramref name="choiceId"/>.</summary>
    public static CareerState ResolveQueued(CareerState s, string choiceId)
        => Engine.ResolveDecision(s, C, choiceId, s.StateVersion).Value;
}
