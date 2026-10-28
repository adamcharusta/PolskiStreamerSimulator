using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Plays M3 careers on <see cref="M3TestContent"/> for rule tests.</summary>
public static class M3Play
{
    public static WeekEngine Engine { get; } = new(new RulesetRegistry([new M1Ruleset(), .. M3Ruleset.Versions]));

    public static ContentSnapshot C => M3TestContent.Valid();

    public static CareerState Fresh(ulong seed = 3)
        => CareerFactory.Create(M3Ruleset.SystemsV1, C.Digest, C.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, seed);

    public static CareerState Start(CareerState s, MainAction action, Category category = Category.Fps)
        => Engine.StartWeek(s, C, new WeekPlan(action, category), s.StateVersion).Value;

    /// <summary>Resolves every live event with its last (guaranteed in test content) choice, then settles.</summary>
    public static (CareerState State, WeekReport Report) SettleAll(CareerState s)
    {
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            s = Engine.ResolveEvent(s, C, Engine.PresentEvent(s, C).Value.Choices[^1].ChoiceId, s.StateVersion).Value;
        }

        return Engine.SettleWeek(s, C, s.StateVersion).Value;
    }

    public static CareerState ResolveQueued(CareerState s, string choiceId)
        => Engine.ResolveDecision(s, C, choiceId, s.StateVersion).Value;

    /// <summary>Re-seeds the career until resolving <paramref name="choiceId"/> gives <paramref name="wanted"/>.</summary>
    public static CareerState ResolveQueuedUntil(CareerState s, string choiceId, RollOutcome wanted)
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var next = ResolveQueued(s with { Rng = Pcg32.Create(seed, CareerFactory.CareerStream) }, choiceId);
            if (next.Carry!.Rolls[^1].Outcome == wanted)
            {
                return next;
            }
        }

        throw new InvalidOperationException($"No seed in 1–200 gives {wanted}.");
    }

    /// <summary>Plays Gambling weeks over seeds until a report has a viral clip.</summary>
    public static (CareerState State, WeekReport Report) UntilViral()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var s = Fresh(seed) with { Followers = 20_000 };
            for (var week = 0; week < 30; week++)
            {
                while (s.PendingDecisions is { Length: > 0 })
                {
                    s = ResolveQueued(s, Engine.PresentDecision(s, C).Value.Choices[0].ChoiceId);
                }

                var settled = SettleAll(Start(s, MainAction.NoAction, Category.Gambling));
                if (settled.Report.Viral is not null)
                {
                    return settled;
                }

                s = settled.State;
            }
        }

        throw new InvalidOperationException("No viral week found.");
    }
}
