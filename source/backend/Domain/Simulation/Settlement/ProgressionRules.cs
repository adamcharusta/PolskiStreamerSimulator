using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation.Settlement;

public static class ProgressionRules
{
    private static readonly Fixed MinimumGrowthFactor = Fixed.FromRatio(1, 5);

    /// <summary>Practice and training slow with skill but never fall below a fifth.</summary>
    public static Fixed GrowthFactor(Fixed skill)
        => FixedMath.Max(MinimumGrowthFactor, (Fixed.Hundred - skill) / Fixed.Hundred);

    /// <summary>
    /// A stream costs energy and adds burnout after the main action, both scaled by <paramref name="factor"/>
    /// (one for a regular stream, less for a short stream).
    /// </summary>
    public static CareerState ApplyStreamFatigue(CareerState s, BalanceConstants b, Fixed factor)
        => s with
        {
            Energy = Stats.Bounded(s.Energy - (b.StreamEnergyCost * factor)),
            Burnout = Stats.Bounded(s.Burnout + (b.StreamBurnoutGain * factor)),
        };

    /// <summary>
    /// M3 only: the streamed category's per-stream credibility and drama effects (the caller reports them as stat lines
    /// with detail <c>category</c> through <see cref="Stats.Changes"/>); unchanged when both are zero.
    /// </summary>
    public static CareerState ApplyCategoryReputation(CareerState s, Category primary, BalanceConstants b)
    {
        var profile = b.Profile(primary);
        if (profile.CredibilityPerStream == Fixed.Zero && profile.DramaPerStream == Fixed.Zero)
        {
            return s;
        }

        var withCredibility = Stats.Add(s, StatKey.Credibility, profile.CredibilityPerStream, primary);
        return Stats.Add(withCredibility, StatKey.Drama, profile.DramaPerStream, primary);
    }

    /// <summary>
    /// The streamed category gains <c>max(0.2, (100 − skill)/100)</c> skill and 2–4 boredom and resets its
    /// inactivity; every other category loses 1–2 boredom, counts one more inactive week and, after
    /// <see cref="BalanceConstants.SkillDecayAfterWeeks"/> inactive weeks, decays toward its starting skill.
    /// </summary>
    public static CareerState ApplySkillAndBoredom(
        CareerState s,
        Category primary,
        CategoryValues<Fixed> startingSkill,
        ref Pcg32 rng,
        BalanceConstants b)
    {
        var skill = s.Skill;
        var boredom = s.Boredom;
        var inactive = s.InactiveWeeks;
        foreach (var category in Enum.GetValues<Category>())
        {
            if (category == primary)
            {
                var master = skill[category] >= b.MasteryFromSkill;
                skill = skill.With(category, Stats.Bounded(skill[category] + GrowthFactor(skill[category])));
                var profile = b.Profile(primary);
                (var gain, rng) = rng.NextInt(profile.BoredomGainMin, profile.BoredomGainMax);
                if (master && b.MasteryBoredomRelief > 0)
                {
                    gain = Math.Max(1, gain - b.MasteryBoredomRelief);
                }

                boredom = boredom.With(category, Stats.Bounded(boredom[category] + Fixed.FromInt(gain)));
                inactive = inactive.With(category, 0);
                continue;
            }

            (var relief, rng) = rng.NextInt(b.BoredomReliefMin, b.BoredomReliefMax);
            boredom = boredom.With(category, Stats.Bounded(boredom[category] - Fixed.FromInt(relief)));
            inactive = inactive.With(category, inactive[category] + 1);
            if (inactive[category] > b.SkillDecayAfterWeeks && skill[category] > startingSkill[category])
            {
                skill = skill.With(category, FixedMath.Max(startingSkill[category], skill[category] - b.SkillDecayPerWeek));
            }
        }

        return s with { Skill = skill, Boredom = boredom, InactiveWeeks = inactive };
    }
}
