using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

public static class OddsCalculator
{
    public const string EasyModifierLabel = "difficulty.easy";

    private static readonly Fixed MinimumProbability = Fixed.FromRatio(5, 100);
    private static readonly Fixed MaximumProbability = Fixed.FromRatio(95, 100);

    /// <summary>
    /// <c>p = clamp(0.05, 0.95, base + Σ min(cap, stat × perPoint) + easy bonus)</c> and
    /// <c>threshold = round(100p)</c>. Primary-category stats read the pending week's plan.
    /// </summary>
    public static ChoiceOdds For(CareerState s, ChoiceDefinition choice, BalanceConstants b)
    {
        if (choice.Guaranteed)
        {
            return new ChoiceOdds(choice.Id, true, Fixed.One, 100, [], choice.AllowsCritical);
        }

        var modifiers = ImmutableArray.CreateBuilder<ModifierLine>();
        foreach (var modifier in choice.Modifiers)
        {
            var value = Stats.Get(s, modifier.Stat, PrimaryCategory(s, modifier.Stat)) * modifier.PerPoint;
            if (modifier.Cap is { } cap)
            {
                value = FixedMath.Min(cap, value);
            }

            modifiers.Add(new ModifierLine(modifier.LabelKey, value));
        }

        if (s.Difficulty == Difficulty.Easy)
        {
            modifiers.Add(new ModifierLine(EasyModifierLabel, b.EasyChanceBonus));
        }

        var probability = modifiers.Aggregate(choice.BaseChance, (sum, line) => sum + line.Value)
            .Clamp(MinimumProbability, MaximumProbability);
        var threshold = (int)(probability * Fixed.Hundred).RoundToLong();
        return new ChoiceOdds(choice.Id, false, probability, threshold, modifiers.ToImmutable(), choice.AllowsCritical);
    }

    private static Category PrimaryCategory(CareerState s, StatKey stat)
        => stat is StatKey.PrimarySkill or StatKey.PrimaryBoredom
            ? s.Pending?.Plan.Category ?? throw new InvalidOperationException("Primary-category odds need a started week.")
            : default;
}
