using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

public enum SponsorConsequenceKind
{
    MissedInstallment,
    Breach,
}

/// <summary>What a plan would cost one deal at settlement: the penalty, the credibility change and whether the deal ends.</summary>
public sealed record SponsorConsequence(
    string ContractId,
    string TemplateId,
    SponsorConsequenceKind Kind,
    long PenaltyPln,
    Fixed Credibility,
    bool Terminates);

/// <summary>
/// M3-B: sponsors — the weekly offer roll, the starting lead, offers opened by effects or a pitch, the answer to an
/// offer and the settlement of accepted deals. Every rule is inert when the content has no
/// <see cref="BalanceConstants.Sponsors"/> section.
/// </summary>
public static class SponsorRules
{
    private const long MagnitudeBase = 1_000;
    private const string RestOnlyFlag = "sys:rest-only";
    private const string SponsorDetail = "sponsor";

    /// <summary>What settlement does with one deal in a week.</summary>
    private enum DealOutcome
    {
        NotDue,
        Paid,
        Postponed,
        Missed,
        Breached,
    }

    /// <summary>
    /// <c>min(cap, base + perMagnitude × magnitude)</c>, where the magnitude counts the <c>k ≥ 1</c> with
    /// <c>followers ≥ 1,000 × 10^k</c>.
    /// </summary>
    public static Fixed OfferChance(long followers, SponsorBalance b)
    {
        var magnitude = 0;
        for (var threshold = MagnitudeBase; followers / 10 >= threshold; threshold *= 10)
        {
            magnitude++;
        }

        return FixedMath.Min(b.OfferCap, b.OfferBase + (b.OfferPerMagnitude * Fixed.FromInt(magnitude)));
    }

    /// <summary><c>min(cap, payment + perThousand × (followers / 1,000))</c> with integer division.</summary>
    public static long Payment(SponsorTemplate t, long followers)
        => Math.Min(t.PaymentCapPln, t.PaymentPln + (t.PaymentPerThousandFollowersPln * (followers / MagnitudeBase)));

    public static SponsorTemplate Template(ContentSnapshot c, string templateId)
        => c.Sponsors.First(t => string.Equals(t.Id, templateId, StringComparison.Ordinal));

    /// <summary>
    /// Templates that may make an offer now, in content order: prerequisites met, the current platform allowed, no
    /// open offer and no active deal for the template.
    /// </summary>
    public static IReadOnlyList<SponsorTemplate> Eligible(CareerState s, ContentSnapshot c)
        => [.. c.Sponsors.Where(t => Unmet(s, t) is null && !HasOffer(s, t.Id) && !HasDeal(s, t.Id))];

    /// <summary>Opens an offer of <paramref name="t"/> in the current week with the payment fixed now.</summary>
    public static CareerState Open(CareerState s, SponsorTemplate t, int expiresAfterWeek)
    {
        var week = s.Week.Value;
        var offer = new SponsorOffer($"{t.Id}:{week}", t.Id, week, expiresAfterWeek, Payment(t, s.Followers));
        return s with { Offers = (s.Offers ?? []).Add(offer) };
    }

    /// <summary>
    /// Opens the named template's offer for <see cref="SponsorBalance.OfferWeeks"/> weeks (a <c>SponsorOffer</c>
    /// effect), unless the template already has an open offer or an active deal.
    /// </summary>
    public static CareerState Offer(CareerState s, ContentSnapshot c, string templateId)
        => c.Balance.Sponsors is { } b && !HasOffer(s, templateId) && !HasDeal(s, templateId)
            ? Open(s, Template(c, templateId), ExpiresAfter(s, b))
            : s;

    /// <summary>The background's starting lead, offered in the first week.</summary>
    public static CareerState Lead(CareerState s, ContentSnapshot c)
        => c.Balance.Sponsors is { } b && b.StartingLeads.TryGetValue(s.Background, out var templateId)
            ? Offer(s, c, templateId)
            : s;

    /// <summary>
    /// After a settlement: removes the offers that expired with <paramref name="settledWeek"/>, then rolls
    /// <see cref="OfferChance"/> and opens one eligible template by weight. Draws nothing when no template is eligible.
    /// </summary>
    public static CareerState AfterSettlement(CareerState s, ContentSnapshot c, int settledWeek, ref Pcg32 rng)
    {
        if (c.Balance.Sponsors is not { } b)
        {
            return s;
        }

        if (s.Offers is { } offers && offers.Any(o => o.ExpiresAfterWeek <= settledWeek))
        {
            s = s with { Offers = offers.RemoveAll(o => o.ExpiresAfterWeek <= settledWeek) };
        }

        var eligible = Eligible(s, c);
        if (eligible.Count == 0)
        {
            return s;
        }

        (var draw, rng) = rng.NextInt(1, 100);
        if (draw > (OfferChance(s.Followers, b) * Fixed.Hundred).RoundToLong())
        {
            return s;
        }

        return OpenByWeight(s, eligible, b, ref rng);
    }

    /// <summary>A successful pitch: opens one eligible template by weight at once; draws nothing when none is eligible.</summary>
    public static CareerState Pitched(CareerState s, ContentSnapshot c, ref Pcg32 rng)
    {
        var eligible = Eligible(s, c);
        return eligible.Count == 0 ? s : OpenByWeight(s, eligible, c.Balance.Sponsors!, ref rng);
    }

    /// <summary>Contract installments due in the current week (one-off deals are not tasks).</summary>
    public static ImmutableArray<SponsorContract> DueThisWeek(CareerState s)
        => s.Contracts is { } deals ? [.. deals.Where(d => d.Kind == SponsorKind.Contract && d.NextDueWeek == s.Week.Value)] : [];

    /// <summary>
    /// The activities of <paramref name="plan"/> sponsors may refuse: <c>gambling</c> for a Gambling stream,
    /// <c>adult-business</c> for side-business work or while the side business is open.
    /// </summary>
    public static ImmutableHashSet<string> Incompatible(CareerState s, WeekPlan plan)
    {
        var activities = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        if (plan.Category == Category.Gambling)
        {
            activities.Add(SponsorTemplate.Gambling);
        }

        if (plan.Action == MainAction.SideBusinessWork || IsSideBusinessOpen(s))
        {
            activities.Add(SponsorTemplate.AdultBusiness);
        }

        return activities.ToImmutable();
    }

    /// <summary>The missed installments and breaches <paramref name="plan"/> would cause at settlement, in deal order.</summary>
    public static ImmutableArray<SponsorConsequence> Consequences(CareerState s, ContentSnapshot c, WeekPlan plan)
    {
        if (s.Contracts is not { Length: > 0 } deals || c.Balance.Sponsors is not { } b)
        {
            return [];
        }

        var incompatible = Incompatible(s, plan);
        var consequences = ImmutableArray.CreateBuilder<SponsorConsequence>();
        foreach (var d in deals)
        {
            var t = Template(c, d.TemplateId);
            var outcome = Outcome(s, plan, d, t, incompatible);
            if (outcome is DealOutcome.Missed or DealOutcome.Breached)
            {
                var missed = outcome == DealOutcome.Missed;
                consequences.Add(new SponsorConsequence(
                    d.ContractId,
                    d.TemplateId,
                    missed ? SponsorConsequenceKind.MissedInstallment : SponsorConsequenceKind.Breach,
                    Penalty(d, t),
                    t.Breach.Credibility,
                    !missed || d.Missed + 1 >= b.MissesToTerminate));
            }
        }

        return consequences.ToImmutable();
    }

    /// <summary>
    /// Settles every deal for the pending week, in list order: an incompatible activity breaches and ends a deal; a due
    /// installment is paid (<c>income:sponsor</c>), postponed by a week off, or missed (<c>cost:sponsor-penalty</c>
    /// capped at the installment and the breach credibility; the last allowed miss ends the contract with
    /// <see cref="SponsorBalance.TerminationCredibility"/> as a separate line). A one-off is paid or breached in its
    /// week. Money lines go into the pending week; credibility changes apply at once with <c>stat:Credibility</c> lines.
    /// </summary>
    public static (CareerState State, PendingWeek Pending) Settle(CareerState s, PendingWeek p, ContentSnapshot c)
    {
        if (s.Contracts is not { Length: > 0 } deals || c.Balance.Sponsors is not { } b)
        {
            return (s, p);
        }

        var incompatible = Incompatible(s, p.Plan);
        var lines = p.Lines.ToBuilder();
        var kept = ImmutableArray.CreateBuilder<SponsorContract>();
        var category = p.Plan.Category;
        foreach (var d in deals)
        {
            var t = Template(c, d.TemplateId);
            switch (Outcome(s, p.Plan, d, t, incompatible))
            {
                case DealOutcome.NotDue:
                    kept.Add(d);
                    break;
                case DealOutcome.Postponed:
                    kept.Add(d with { NextDueWeek = d.NextDueWeek + 1, TermEndWeek = d.TermEndWeek + 1 });
                    break;
                case DealOutcome.Paid:
                    lines.Add(new ReportLine("income:sponsor", AmountPln: d.PaymentPln, Detail: t.Id));
                    AddIfRunning(kept, d with { NextDueWeek = d.NextDueWeek + (t.DueEveryWeeks ?? 0) });
                    break;
                case DealOutcome.Breached:
                    s = Breach(s, d, t, category, lines);
                    break;
                default: // DealOutcome.Missed
                    s = Breach(s, d, t, category, lines);
                    var missed = d with { Missed = d.Missed + 1, NextDueWeek = d.NextDueWeek + t.DueEveryWeeks!.Value };
                    if (missed.Missed >= b.MissesToTerminate)
                    {
                        s = Credibility(s, b.TerminationCredibility, category, lines);
                    }
                    else
                    {
                        AddIfRunning(kept, missed);
                    }

                    break;
            }
        }

        return (s with { Contracts = kept.ToImmutable() }, p with { Lines = lines.ToImmutable() });
    }

    /// <summary>Why <paramref name="offer"/> cannot be accepted now, or <c>null</c> when it can.</summary>
    public static string? AcceptRejection(CareerState s, ContentSnapshot c, SponsorOffer offer)
    {
        var deals = s.Contracts ?? [];
        if (deals.Length >= c.Balance.Sponsors!.MaxContracts)
        {
            return "contract-limit";
        }

        return HasDeal(s, offer.TemplateId)
            ? "same-sponsor"
            : Unmet(s, Template(c, offer.TemplateId)) is { } unmet ? $"prerequisite:{unmet}" : null;
    }

    /// <summary>
    /// Answers an open offer: accepting turns it into a deal, declining drops it. A one-off is due this week; a
    /// contract's first installment is due after <see cref="SponsorTemplate.DueEveryWeeks"/> weeks.
    /// </summary>
    public static CareerState Respond(CareerState s, ContentSnapshot c, SponsorOffer offer, bool accept)
    {
        var state = s with { Offers = s.Offers!.Value.Remove(offer) };
        if (!accept)
        {
            return state;
        }

        var t = Template(c, offer.TemplateId);
        var week = s.Week.Value;
        var contract = t.Kind == SponsorKind.OneOff
            ? new SponsorContract(offer.OfferId, t.Id, t.Kind, week, week, week, offer.PaymentPln, 0)
            : new SponsorContract(offer.OfferId, t.Id, t.Kind, week, week + t.DueEveryWeeks!.Value - 1, week + t.TermWeeks!.Value - 1, offer.PaymentPln, 0);
        return state with { Contracts = (s.Contracts ?? []).Add(contract) };
    }

    /// <summary>The first prerequisite of <paramref name="t"/> the career does not meet now, or <c>null</c>.</summary>
    private static string? Unmet(CareerState s, SponsorTemplate t)
    {
        var p = t.Prerequisites;
        if (s.Followers < p.MinFollowers)
        {
            return "followers";
        }

        if (p.MinCredibility is { } minCredibility && s.Credibility < minCredibility)
        {
            return "credibility";
        }

        if ((p.MaxDrama is { } maxDrama && s.Drama > maxDrama) || (p.MinDrama is { } minDrama && s.Drama < minDrama))
        {
            return "drama";
        }

        if (t.Platforms.Length > 0 && !t.Platforms.Contains(Economy.Current(s)))
        {
            return "platform";
        }

        return t.IncompatibleWith.Contains(SponsorTemplate.AdultBusiness) && IsSideBusinessOpen(s)
            ? SponsorTemplate.AdultBusiness
            : null;
    }

    private static DealOutcome Outcome(CareerState s, WeekPlan plan, SponsorContract d, SponsorTemplate t, ImmutableHashSet<string> incompatible)
    {
        if (t.IncompatibleWith.Any(incompatible.Contains))
        {
            return DealOutcome.Breached;
        }

        if (d.NextDueWeek != s.Week.Value)
        {
            return DealOutcome.NotDue;
        }

        if (CareerFlags.IsActive(s, RestOnlyFlag))
        {
            return DealOutcome.Postponed;
        }

        var category = t.RequiredCategory is not { } required || required == plan.Category;
        if (d.Kind == SponsorKind.OneOff)
        {
            return category ? DealOutcome.Paid : DealOutcome.Breached;
        }

        return category && plan.Action == MainAction.PitchFulfillSponsor ? DealOutcome.Paid : DealOutcome.Missed;
    }

    /// <summary>The breach penalty, never more than the installment.</summary>
    private static long Penalty(SponsorContract d, SponsorTemplate t) => Math.Min(t.Breach.CashPln, d.PaymentPln);

    private static CareerState Breach(CareerState s, SponsorContract d, SponsorTemplate t, Category category, ImmutableArray<ReportLine>.Builder lines)
    {
        if (Penalty(d, t) is > 0 and var penalty)
        {
            lines.Add(new ReportLine("cost:sponsor-penalty", AmountPln: penalty, Detail: t.Id));
        }

        return Credibility(s, t.Breach.Credibility, category, lines);
    }

    private static CareerState Credibility(CareerState s, Fixed delta, Category category, ImmutableArray<ReportLine>.Builder lines)
    {
        var after = Stats.Add(s, StatKey.Credibility, delta, category);
        lines.AddRange(Stats.Changes(s, after, SponsorDetail));
        return after;
    }

    /// <summary>Keeps a contract whose next installment still falls within its term; a one-off never runs on.</summary>
    private static void AddIfRunning(ImmutableArray<SponsorContract>.Builder kept, SponsorContract d)
    {
        if (d.Kind == SponsorKind.Contract && d.NextDueWeek <= d.TermEndWeek)
        {
            kept.Add(d);
        }
    }

    /// <summary>Until the side-business rules exist, a business with any quality counts as open.</summary>
    private static bool IsSideBusinessOpen(CareerState s) => s.SideBusiness is { Quality.Raw: > 0 };

    private static CareerState OpenByWeight(CareerState s, IReadOnlyList<SponsorTemplate> eligible, SponsorBalance b, ref Pcg32 rng)
    {
        (var ticket, rng) = rng.NextInt(1, eligible.Sum(t => t.Weight));
        return Open(s, PickByWeight(eligible, ticket), ExpiresAfter(s, b));
    }

    private static bool HasOffer(CareerState s, string templateId)
        => s.Offers is { } offers && offers.Any(o => string.Equals(o.TemplateId, templateId, StringComparison.Ordinal));

    private static bool HasDeal(CareerState s, string templateId)
        => s.Contracts is { } deals && deals.Any(d => string.Equals(d.TemplateId, templateId, StringComparison.Ordinal));

    private static int ExpiresAfter(CareerState s, SponsorBalance b) => s.Week.Value + b.OfferWeeks - 1;

    /// <summary>The template whose cumulative weight, in content order, first reaches <paramref name="ticket"/>.</summary>
    private static SponsorTemplate PickByWeight(IReadOnlyList<SponsorTemplate> templates, int ticket)
    {
        var cumulative = 0;
        foreach (var t in templates)
        {
            cumulative += t.Weight;
            if (ticket <= cumulative)
            {
                return t;
            }
        }

        return templates[^1];
    }
}
