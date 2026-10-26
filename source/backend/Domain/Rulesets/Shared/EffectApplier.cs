using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.Shared;

/// <summary>Applies event effects to a career and its pending week or its decision carry.</summary>
public static class EffectApplier
{
    /// <summary>
    /// Live events: stat, skill, flag and follow-up effects change the career now; cash, audience, quality, follower
    /// and viral effects accumulate in the pending week for settlement.
    /// </summary>
    public static CareerState ToWeek(CareerState s, string sourceId, ImmutableArray<Effect> effects, ref PendingWeek pending)
    {
        var before = s;
        var lines = pending.Lines.ToBuilder();
        foreach (var effect in effects)
        {
            switch (effect.Kind)
            {
                case EffectKind.CashDelta:
                    var amount = effect.Amount.RoundToLong();
                    pending = pending with { CashDeltaFromEvents = pending.CashDeltaFromEvents + amount };
                    lines.Add(CashLine(amount, sourceId));
                    break;
                case EffectKind.AudienceMultiplier:
                    pending = pending with { AudienceMultiplier = pending.AudienceMultiplier * effect.Amount };
                    break;
                case EffectKind.EventQuality:
                    pending = pending with { EventQuality = pending.EventQuality + effect.Amount };
                    break;
                case EffectKind.FollowersPercent:
                    pending = pending with { FollowersPercent = (pending.FollowersPercent ?? Fixed.Zero) + effect.Amount };
                    break;
                case EffectKind.ViralChance:
                    pending = pending with { ViralChance = (pending.ViralChance ?? Fixed.Zero) + effect.Amount };
                    break;
                case EffectKind.PolicyStrike or EffectKind.GlobalBan or EffectKind.SponsorOffer:
                    throw NotYetApplied(effect);
                default:
                    s = ToCareer(s, effect, pending.Plan.Category);
                    break;
            }
        }

        lines.AddRange(Stats.Changes(before, s, sourceId));
        pending = pending with { Lines = lines.ToImmutable() };
        return s;
    }

    /// <summary>
    /// Decisions between weeks: stat, skill, flag and follow-up effects change the career now; cash and follower
    /// effects and the stat-change lines go into the decision carry for the next week's report. Primary-category
    /// stats use the last streamed category. Audience, quality and viral effects are rejected (the content validator
    /// forbids them in decisions).
    /// </summary>
    public static CareerState ToCarry(CareerState s, string sourceId, ImmutableArray<Effect> effects)
    {
        var before = s;
        var carry = s.Carry ?? DecisionCarry.Empty;
        var lines = carry.Lines.ToBuilder();
        var primary = Stats.LastPrimary(s);
        foreach (var effect in effects)
        {
            switch (effect.Kind)
            {
                case EffectKind.CashDelta:
                    var amount = effect.Amount.RoundToLong();
                    carry = carry with { CashPln = carry.CashPln + amount };
                    lines.Add(CashLine(amount, sourceId));
                    break;
                case EffectKind.FollowersPercent:
                    carry = carry with { FollowersPercent = carry.FollowersPercent + effect.Amount };
                    break;
                case EffectKind.AudienceMultiplier or EffectKind.EventQuality or EffectKind.ViralChance:
                    throw new ArgumentOutOfRangeException(nameof(effects), effect.Kind, "Not allowed in a decision.");
                case EffectKind.PolicyStrike or EffectKind.GlobalBan or EffectKind.SponsorOffer:
                    throw NotYetApplied(effect);
                default:
                    s = ToCareer(s, effect, primary);
                    break;
            }
        }

        lines.AddRange(Stats.Changes(before, s, sourceId));
        return s with { Carry = carry with { Lines = lines.ToImmutable() } };
    }

    /// <summary>The effects that change the career itself, whenever they are applied.</summary>
    private static CareerState ToCareer(CareerState s, Effect effect, Category primary)
    {
        switch (effect.Kind)
        {
            case EffectKind.StatDelta:
                return Stats.Add(s, effect.Stat!.Value, effect.Amount, primary);
            case EffectKind.SkillDelta:
                var category = effect.Category!.Value;
                return s with { Skill = s.Skill.With(category, Stats.Bounded(s.Skill[category] + effect.Amount)) };
            case EffectKind.SetFlag:
                return CareerFlags.Set(s, effect.Target!, effect.Weeks);
            case EffectKind.ClearFlag:
                return CareerFlags.Clear(s, effect.Target!);
            case EffectKind.FollowUp:
                return s with { Scheduled = (s.Scheduled ?? []).Add(new ScheduledEvent(effect.Target!, s.Week.Value + effect.Weeks!.Value)) };
            default:
                throw new ArgumentOutOfRangeException(nameof(effect), effect.Kind, null);
        }
    }

    /// <summary>The M3-B effect kinds are read from content but applied only once their rules exist; no content uses them yet.</summary>
    private static ArgumentOutOfRangeException NotYetApplied(Effect effect)
        => new(nameof(effect), effect.Kind, "The M3-B effect kinds are not applied yet.");

    private static ReportLine CashLine(long amount, string sourceId)
        => new(amount >= 0 ? "income:event" : "cost:event", AmountPln: Math.Abs(amount), Detail: sourceId);
}
