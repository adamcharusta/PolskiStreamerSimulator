using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.Shared;

/// <summary>Applies event effects to a career and its pending week.</summary>
public static class EffectApplier
{
    /// <summary>Stat effects change the career now; cash, audience and quality effects accumulate for settlement.</summary>
    public static CareerState ToWeek(CareerState s, string eventId, ImmutableArray<Effect> effects, ref PendingWeek pending)
    {
        var before = s;
        var lines = pending.Lines.ToBuilder();
        foreach (var effect in effects)
        {
            switch (effect.Kind)
            {
                case EffectKind.StatDelta:
                    s = Stats.Add(s, effect.Stat!.Value, effect.Amount, pending.Plan.Category);
                    break;
                case EffectKind.CashDelta:
                    var amount = effect.Amount.RoundToLong();
                    pending = pending with { CashDeltaFromEvents = pending.CashDeltaFromEvents + amount };
                    lines.Add(new ReportLine(amount >= 0 ? "income:event" : "cost:event", AmountPln: Math.Abs(amount), Detail: eventId));
                    break;
                case EffectKind.AudienceMultiplier:
                    pending = pending with { AudienceMultiplier = pending.AudienceMultiplier * effect.Amount };
                    break;
                case EffectKind.EventQuality:
                    pending = pending with { EventQuality = pending.EventQuality + effect.Amount };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effects), effect.Kind, null);
            }
        }

        lines.AddRange(Stats.Changes(before, s, eventId));
        pending = pending with { Lines = lines.ToImmutable() };
        return s;
    }
}
