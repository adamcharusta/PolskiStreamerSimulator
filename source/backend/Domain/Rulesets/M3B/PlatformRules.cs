using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

/// <summary>M3-B: the platform the career streams on and what it changes in the week's balance.</summary>
public static class PlatformRules
{
    /// <summary>
    /// The balance with the current platform's factors in place of the scalar platform values (reach times the
    /// category's reach on that platform); <paramref name="b"/> itself when the balance has no platforms.
    /// </summary>
    public static BalanceConstants Effective(BalanceConstants b, CareerState s, Category category)
    {
        if (b.Platforms is not { } platforms)
        {
            return b;
        }

        var profile = platforms[Economy.Current(s)];
        return b with
        {
            PlatformReach = profile.Reach * profile.CategoryReach[category],
            PlatformSubYield = profile.SubYield,
            PlatformAdYield = profile.AdYield,
        };
    }
}
