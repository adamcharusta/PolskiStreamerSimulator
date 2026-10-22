using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

/// <summary>
/// M3-B: sponsor offers — the weekly offer roll, the starting lead, offers opened by effects and the answer to an
/// offer. Every rule is inert when the content has no <see cref="BalanceConstants.Sponsors"/> section.
/// </summary>
public static class SponsorRules
{
    private const long MagnitudeBase = 1_000;

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

        (var ticket, rng) = rng.NextInt(1, eligible.Sum(t => t.Weight));
        return Open(s, PickByWeight(eligible, ticket), ExpiresAfter(s, b));
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

        return t.IncompatibleWith.Contains(SponsorTemplate.AdultBusiness) && s.SideBusiness is { Quality.Raw: > 0 }
            ? SponsorTemplate.AdultBusiness
            : null;
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
