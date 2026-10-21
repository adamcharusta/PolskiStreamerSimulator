using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

public static class OddsCalculator
{
    public const string EasyModifierLabel = "difficulty.easy";
    public const string BurnoutModifierLabel = "modifier.burnout";

    private static readonly Fixed MinimumProbability = Fixed.FromRatio(5, 100);
    private static readonly Fixed MaximumProbability = Fixed.FromRatio(95, 100);

    /// <summary>
    /// <c>p = clamp(0.05, 0.95, base + Σ min(cap, stat × perPoint) + easy bonus)</c> and
    /// <c>threshold = round(100p)</c>. <paramref name="primary"/> overrides the category primary-category stats read (a plan not yet started). Primary-category stats read the pending week's plan (see <see cref="PrimaryCategory"/>).
    /// </summary>
    public static ChoiceOdds For(CareerState s, ChoiceDefinition choice, BalanceConstants b, Category? primary = null)
    {
        if (choice.Guaranteed)
        {
            return new ChoiceOdds(choice.Id, true, Fixed.One, 100, [], choice.AllowsCritical, ViralOnCritical(choice, b));
        }

        var modifiers = ImmutableArray.CreateBuilder<ModifierLine>();
        foreach (var modifier in choice.Modifiers)
        {
            var value = Stats.Get(s, modifier.Stat, PrimaryCategory(s, modifier.Stat, primary)) * modifier.PerPoint;
            if (modifier.Cap is { } cap)
            {
                value = FixedMath.Min(cap, value);
            }

            modifiers.Add(new ModifierLine(modifier.LabelKey, value));
        }

        if (s.Burnout > b.BurnoutPenaltyFrom)
        {
            modifiers.Add(new ModifierLine(BurnoutModifierLabel, -FixedMath.Min(b.BurnoutPenaltyCap, (s.Burnout - b.BurnoutPenaltyFrom) * b.BurnoutPenaltyPerPoint)));
        }

        if (s.Difficulty == Difficulty.Easy)
        {
            modifiers.Add(new ModifierLine(EasyModifierLabel, b.EasyChanceBonus));
        }

        var probability = modifiers.Aggregate(choice.BaseChance, (sum, line) => sum + line.Value)
            .Clamp(MinimumProbability, MaximumProbability);
        var threshold = (int)(probability * Fixed.Hundred).RoundToLong();
        return new ChoiceOdds(choice.Id, false, probability, threshold, modifiers.ToImmutable(), choice.AllowsCritical, ViralOnCritical(choice, b));
    }

    /// <summary>The viral chance a critical outcome of <paramref name="choice"/> can add (zero when it has no critical).</summary>
    private static Fixed ViralOnCritical(ChoiceDefinition choice, BalanceConstants b)
        => choice.AllowsCritical ? b.ViralPerCritical : Fixed.Zero;

    /// <summary>
    /// The category primary-category stats read: the pending week's plan, or — for a decision between weeks — the
    /// category of the last settled week (<see cref="Category.JustChatting"/> before the first one).
    /// </summary>
    private static Category PrimaryCategory(CareerState s, StatKey stat, Category? explicitPrimary)
        => stat is StatKey.PrimarySkill or StatKey.PrimaryBoredom
            ? explicitPrimary ?? s.Pending?.Plan.Category ?? Stats.LastPrimary(s)
            : default;
}
