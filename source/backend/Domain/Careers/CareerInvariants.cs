using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Careers;

public static class CareerInvariants
{
    public const int MaxEquipmentTier = 4;

    /// <summary>Most sponsor contracts a career may hold at once.</summary>
    public const int MaxContracts = 2;

    /// <summary>ID of the decision that answers a ban of the current platform.</summary>
    public const string PlatformBanDecision = "platform-ban";

    /// <summary>Cash at or below this value cannot belong to an active career (insolvency).</summary>
    public const long InsolvencyThresholdPln = -1_000;

    /// <summary>Returns every violated invariant as a stable code, in a fixed order; empty when the state is valid.</summary>
    public static IReadOnlyList<string> Validate(CareerState s)
    {
        var violations = new List<string>();
        if (s.Week.Value is < CareerWeek.First or > CareerWeek.Last)
        {
            violations.Add("week-range");
        }

        CheckStats(violations, s);
        if (s.Followers < 0)
        {
            violations.Add("followers-negative");
        }

        if (s.LastLiveViewers < 0)
        {
            violations.Add("viewers-negative");
        }

        violations.AddRange(Enum.GetValues<EquipmentSlot>()
            .Where(slot => !s.Equipment.TryGetValue(slot, out var tier) || tier is < 0 or > MaxEquipmentTier)
            .Select(slot => $"equipment-tier:{slot}"));
        if (s.Status == CareerStatus.Ended)
        {
            CheckEnded(violations, s);
        }
        else
        {
            CheckActive(violations, s);
        }

        if (s.PendingDecisions is { Length: > 0 } && s.Pending is not null)
        {
            violations.Add("decision-during-week");
        }

        if (s.Flags is { } flags && flags.Any(f => f.Value < s.LastSettledWeek))
        {
            violations.Add("expired-flag-kept");
        }

        CheckEconomy(violations, s);
        return violations;
    }

    private static void CheckEconomy(List<string> violations, CareerState s)
    {
        if (s.Contracts is { Length: > MaxContracts })
        {
            violations.Add("contract-limit");
        }

        if (s.Offers is { } offers && offers.Select(o => o.TemplateId).Distinct(StringComparer.Ordinal).Count() != offers.Length)
        {
            violations.Add("offer-duplicate");
        }

        if (s.Status == CareerStatus.Active
            && s.Pending is null
            && Economy.IsBanned(s, Economy.Current(s))
            && s.PendingDecisions?.Contains(PlatformBanDecision, StringComparer.Ordinal) != true)
        {
            violations.Add("banned-platform-unanswered");
        }

        if (s.SideBusiness is { } business && (business.Quality < Fixed.Zero || business.Quality > Fixed.Hundred))
        {
            violations.Add("side-business-quality");
        }

        if (s.FormerFollowers < 0)
        {
            violations.Add("former-followers-negative");
        }
    }

    private static void CheckStats(List<string> violations, CareerState s)
    {
        CheckStat(violations, "Drama", s.Drama);
        CheckStat(violations, "Credibility", s.Credibility);
        CheckStat(violations, "Energy", s.Energy);
        CheckStat(violations, "Burnout", s.Burnout);
        foreach (var category in Enum.GetValues<Category>())
        {
            CheckStat(violations, $"Skill.{category}", s.Skill[category]);
        }

        foreach (var category in Enum.GetValues<Category>())
        {
            CheckStat(violations, $"Boredom.{category}", s.Boredom[category]);
        }
    }

    private static void CheckEnded(List<string> violations, CareerState s)
    {
        if (s.Ending is null)
        {
            violations.Add("ended-without-reason");
        }

        if (s.FinalScore is null)
        {
            violations.Add("ended-without-score");
        }

        if (s.Pending is not null)
        {
            violations.Add("pending-on-ended");
        }
    }

    private static void CheckActive(List<string> violations, CareerState s)
    {
        if (s.Ending is not null || s.FinalScore is not null)
        {
            violations.Add("active-with-ending");
        }

        if (s.CashPln <= InsolvencyThresholdPln)
        {
            violations.Add("active-insolvent");
        }
    }

    private static void CheckStat(List<string> violations, string name, Fixed value)
    {
        if (value < Fixed.Zero || value > Fixed.Hundred)
        {
            violations.Add($"stat-range:{name}");
        }
    }
}
