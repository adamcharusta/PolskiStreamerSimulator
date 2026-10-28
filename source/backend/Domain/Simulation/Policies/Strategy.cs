using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Simulation.Policies;

/// <summary>How a strategy picks the week's category.</summary>
public enum CategoryPlan
{
    /// <summary>The background's strongest starting skill, switching to the least bored category while it is boring.</summary>
    Strongest,

    /// <summary>One fixed category (<see cref="Strategy.Focus"/>).</summary>
    Focus,

    /// <summary>The category rotates with the week number.</summary>
    Rotate,
}

/// <summary>How much a strategy gambles on event choices and how much drama it tolerates.</summary>
public enum RiskAppetite
{
    Safe,
    Balanced,
    Bold,
}

/// <summary>
/// M3-C: the player's plan for automatic weeks (spec 2026-10-26 §5): category plan, the free week's routine, rest
/// thresholds, a cash reserve no upgrade or wager spends, risk appetite, sponsor acceptance, a wager tier and the side
/// business. <see cref="Errors"/> lists what makes it invalid.
/// </summary>
public sealed record Strategy(
    CategoryPlan CategoryPlan,
    Category? Focus,
    ImmutableArray<MainAction> Routine,
    int RestBelowEnergy,
    int RestAboveBurnout,
    long ReservePln,
    bool Upgrades,
    RiskAppetite Risk,
    bool AcceptOffers,
    WagerTier? Wager,
    bool SideBusiness)
{
    public const int MaxRoutine = 8;
    public const int MaxRestBelowEnergy = 80;
    public const int MinRestAboveBurnout = 50;
    public const long MaxReservePln = 1_000_000;

    /// <summary>The actions a routine may hold: the free week's choices, never rest, upgrades, the side business or a move.</summary>
    public static ImmutableHashSet<MainAction> RoutineActions { get; } =
        [MainAction.NoAction, MainAction.TrainCategory, MainAction.ManageCommunity, MainAction.NetworkCollaborate, MainAction.PitchFulfillSponsor];

    /// <summary>The strategy the batch dialog starts from: close to <c>Balanced</c>.</summary>
    public static Strategy Default { get; } = new(
        CategoryPlan.Strongest,
        null,
        [MainAction.NoAction, MainAction.NoAction, MainAction.NoAction, MainAction.TrainCategory],
        30,
        70,
        0,
        true,
        RiskAppetite.Balanced,
        true,
        null,
        false);

    /// <summary>Stable error codes, in field order; empty for a valid strategy.</summary>
    public IReadOnlyList<string> Errors()
    {
        var errors = new List<string>();
        if (CategoryPlan == CategoryPlan.Focus && Focus is null)
        {
            errors.Add("focus-missing");
        }

        if (CategoryPlan != CategoryPlan.Focus && Focus is not null)
        {
            errors.Add("focus-unexpected");
        }

        if (Routine.IsDefault || Routine.Length is 0 or > MaxRoutine)
        {
            errors.Add("routine-length");
        }
        else if (!Routine.All(RoutineActions.Contains))
        {
            errors.Add("routine-action");
        }

        if (RestBelowEnergy is < 0 or > MaxRestBelowEnergy)
        {
            errors.Add("energy-range");
        }

        if (RestAboveBurnout is < MinRestAboveBurnout or > 100)
        {
            errors.Add("burnout-range");
        }

        if (ReservePln is < 0 or > MaxReservePln)
        {
            errors.Add("reserve-range");
        }

        return errors;
    }

    public bool Equals(Strategy? other)
        => other is not null
            && CategoryPlan == other.CategoryPlan
            && Focus == other.Focus
            && Routine.SequenceEqual(other.Routine)
            && RestBelowEnergy == other.RestBelowEnergy
            && RestAboveBurnout == other.RestAboveBurnout
            && ReservePln == other.ReservePln
            && Upgrades == other.Upgrades
            && Risk == other.Risk
            && AcceptOffers == other.AcceptOffers
            && Wager == other.Wager
            && SideBusiness == other.SideBusiness;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CategoryPlan);
        hash.Add(Focus);
        foreach (var action in Routine.IsDefault ? [] : Routine)
        {
            hash.Add(action);
        }

        hash.Add(RestBelowEnergy);
        hash.Add(ReservePln);
        hash.Add(Risk);
        return hash.ToHashCode();
    }
}
