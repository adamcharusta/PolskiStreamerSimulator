using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Plays M3-B careers (<c>m3-systems.2</c>) on <see cref="M3BTestContent"/> for economy rule tests.</summary>
public static class M3BPlay
{
    public static WeekEngine Engine => M3Play.Engine;

    public static ContentSnapshot C => M3BTestContent.Valid();

    public static CareerState Fresh(ulong seed = 3, Background background = Background.DormRoomNewcomer)
        => CareerFactory.Create(C, background, Difficulty.Normal, PronounSet.Man, seed);

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

    /// <summary>Accepts or declines an open offer between weeks.</summary>
    public static CareerState Respond(CareerState s, string offerId, bool accept)
        => Engine.RespondOffer(s, C, offerId, accept, s.StateVersion).Value;

    /// <summary>Answers every queued decision with its first available choice.</summary>
    public static CareerState AnswerDecisions(CareerState s)
    {
        while (s.PendingDecisions is { Length: > 0 })
        {
            var choice = Engine.PresentDecision(s, C).Value.Choices.First(o => o.UnavailableReason is null);
            s = Engine.ResolveDecision(s, C, choice.ChoiceId, s.StateVersion).Value;
        }

        return s;
    }

    /// <summary>
    /// One random week: answers the queued decisions and every open offer at random, then starts a random enabled plan,
    /// resolves its events at random and settles. <c>BeforeStart</c> is the state after the decisions and offers.
    /// </summary>
    public static (CareerState BeforeStart, CareerState After, WeekReport Report) RandomWeek(CareerState s, ref Pcg32 rng)
    {
        while (s.PendingDecisions is { Length: > 0 })
        {
            var choices = Engine.PresentDecision(s, C).Value.Choices.Where(o => o.UnavailableReason is null).ToList();
            (var pick, rng) = rng.NextInt(0, choices.Count - 1);
            s = Engine.ResolveDecision(s, C, choices[pick].ChoiceId, s.StateVersion).Value;
        }

        foreach (var offer in s.Offers ?? [])
        {
            (var accept, rng) = rng.NextInt(0, 1);
            var answered = Engine.RespondOffer(s, C, offer.OfferId, accept == 1, s.StateVersion);
            s = answered.IsSuccess ? answered.Value : Respond(s, offer.OfferId, accept: false);
        }

        var plans = Engine.LegalPlans(s, C).Value.Where(p => p.DisabledReason is null).ToList();
        (var plan, rng) = rng.NextInt(0, plans.Count - 1);
        var started = Engine.StartWeek(s, C, plans[plan].Plan, s.StateVersion).Value;
        while (started.Pending!.Resolved.Length < started.Pending.EventIds.Length)
        {
            var choices = Engine.PresentEvent(started, C).Value.Choices.Where(o => o.UnavailableReason is null).ToList();
            (var pick, rng) = rng.NextInt(0, choices.Count - 1);
            started = Engine.ResolveEvent(started, C, choices[pick].ChoiceId, started.StateVersion).Value;
        }

        var (after, report) = Engine.SettleWeek(started, C, started.StateVersion).Value;
        return (s, after, report);
    }

    /// <summary>Answers the first queued decision with <paramref name="choiceId"/>.</summary>
    public static CareerState ResolveQueued(CareerState s, string choiceId)
        => Engine.ResolveDecision(s, C, choiceId, s.StateVersion).Value;
}
