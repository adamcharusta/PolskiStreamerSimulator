using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

/// <summary>M3-B: the platform the career streams on and what it changes in the week's balance.</summary>
public static class PlatformRules
{
    /// <summary>The ID of the ruleset-built decision that opens when the current platform is banned.</summary>
    public const string BanDecisionId = "platform-ban";

    /// <summary>The platforms the career could move to: neither banned nor the current one, in enum order.</summary>
    public static ImmutableArray<Platform> Accessible(CareerState s)
        => [.. Enum.GetValues<Platform>().Where(p => p != Economy.Current(s) && !Economy.IsBanned(s, p))];

    /// <summary>True when the current platform is banned and another platform is still open.</summary>
    public static bool MustMove(CareerState s)
        => Economy.IsBanned(s, Economy.Current(s)) && !Accessible(s).IsEmpty;

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
