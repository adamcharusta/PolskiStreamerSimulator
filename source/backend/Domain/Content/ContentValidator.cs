using System.Collections.Frozen;
using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>
/// Rejects content that a career could not safely run on, before any career starts. Schema-1 (M1) content gets the
/// original checks plus the M3 balance-range checks (its neutral defaults pass them); schema-2 content additionally gets ICU text, flag, follow-up, phase and per-category checks.
/// </summary>
public static class ContentValidator
{
    /// <summary>Pseudo event ID under which the balance's network choice is reported.</summary>
    public const string NetworkChoiceOwner = "balance.networkCollaborate";

    /// <summary>Pseudo event ID under which the balance's sponsor pitch choice is reported.</summary>
    public const string PitchChoiceOwner = "balance.sponsors.pitch";

    /// <summary>Flags the engine sets itself, so content may require them without any event setting them (flags starting with <c>sys:</c> count too).</summary>
    public static readonly FrozenSet<string> EngineFlags = new[] { "viral-clip", "side-business" }.ToFrozenSet(StringComparer.Ordinal);

    private const string SystemFlagPrefix = "sys:";
    private const long MaxCashEffectPln = 1_000_000;

    private static readonly HashSet<EffectKind> DecisionForbidden =
        [EffectKind.AudienceMultiplier, EffectKind.EventQuality, EffectKind.ViralChance];

    /// <summary>The values a sponsor template may list in <see cref="SponsorTemplate.IncompatibleWith"/>.</summary>
    private static readonly FrozenSet<string> Incompatibilities =
        new[] { SponsorTemplate.Gambling, SponsorTemplate.AdultBusiness }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlyList<string> Validate(ContentSnapshot c)
    {
        var violations = new List<string>();
        ValidateBalance(c.Balance, violations);

        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in c.Events)
        {
            ValidateEvent(e, c.Schema, eventIds, violations);
        }

        if (c.Balance.NetworkCollaborate is { } network)
        {
            ValidateChoice(NetworkChoiceOwner, network, c.Schema, new HashSet<string>(StringComparer.Ordinal), violations);
        }

        if (c.Balance.Sponsors is { } sponsors)
        {
            ValidateChoice(PitchChoiceOwner, sponsors.Pitch, c.Schema, new HashSet<string>(StringComparer.Ordinal), violations);
        }

        switch (c.Schema)
        {
            case 1:
                ValidateWeeklySupply(c, violations);
                break;
            case 2:
                ValidateSchemaTwo(c, violations);
                break;
            default:
                violations.Add($"schema:{c.Schema}");
                break;
        }

        violations.AddRange(Enum.GetValues<Background>()
            .Where(background => !c.Profiles.ContainsKey(background))
            .Select(background => $"missing-profile:{background}"));
        return violations;
    }

    private static void ValidateBalance(BalanceConstants b, List<string> violations)
    {
        if (b.EventsPerWeekMin < 1 || b.EventsPerWeekMin > b.EventsPerWeekMax)
        {
            violations.Add("balance:events-per-week");
        }

        if (b.EquipmentPricePln.Length != CareerInvariants.MaxEquipmentTier + 1
            || b.EquipmentUpkeepPerTierPln.Length != CareerInvariants.MaxEquipmentTier + 1)
        {
            violations.Add("balance:equipment-tiers");
        }

        AddUnless(IsSafeViral(b), "balance:viral", violations);
        AddUnless(IsFraction(b.ShortStreamFatigue) && IsFraction(b.ShortStreamAudience), "balance:short-stream", violations);
        AddUnless(
            b.BurnoutPenaltyFrom >= Fixed.Zero && b.BurnoutPenaltyPerPoint >= Fixed.Zero && b.BurnoutPenaltyCap >= Fixed.Zero,
            "balance:burnout-penalty",
            violations);
        AddUnless(b.MasteryBoredomRelief >= 0 && b.MasteryChurnFactor > Fixed.Zero, "balance:mastery", violations);
        if (b.Categories is { } categories)
        {
            violations.AddRange(Enum.GetValues<Category>()
                .Where(category => !IsSafeCategory(categories[category], b.ViralChanceCap))
                .Select(category => $"balance:category:{category}"));
        }
    }

    private static void AddUnless(bool valid, string violation, List<string> violations)
    {
        if (!valid)
        {
            violations.Add(violation);
        }
    }

    /// <summary>A chance cap in [0, 1], shares in [0, 1], a non-negative follower floor and, when clips can happen, multipliers of at least one in order.</summary>
    private static bool IsSafeViral(BalanceConstants b)
        => IsShare(b.ViralChanceCap)
            && IsShare(b.ViralFollowerShare)
            && IsShare(b.ViralViewerShare)
            && b.ViralFollowerMin >= 0
            && (b.ViralChanceCap == Fixed.Zero || (b.ViralMultiplierMin >= Fixed.One && b.ViralMultiplierMin <= b.ViralMultiplierMax));

    /// <summary>Positive factors, ordered noise and boredom ranges, and a viral base the viral chance cap still bounds.</summary>
    private static bool IsSafeCategory(CategoryProfile p, Fixed viralChanceCap)
        => p.NoiseMin > Fixed.Zero && p.NoiseMin <= p.NoiseMax
            && p.BoredomGainMin >= 0 && p.BoredomGainMin <= p.BoredomGainMax
            && new[] { p.Reach, p.Conversion, p.Churn, p.SubYield, p.DonationYield, p.AdYield, p.QualitySkillWeight }.All(factor => factor > Fixed.Zero)
            && p.ViralBase >= Fixed.Zero && p.ViralBase <= viralChanceCap;

    /// <summary>In [0, 1].</summary>
    private static bool IsShare(Fixed value) => value >= Fixed.Zero && value <= Fixed.One;

    /// <summary>In (0, 1].</summary>
    private static bool IsFraction(Fixed value) => value > Fixed.Zero && value <= Fixed.One;

    private static void ValidateEvent(EventDefinition e, int schema, HashSet<string> eventIds, List<string> violations)
    {
        if (!eventIds.Add(e.Id))
        {
            violations.Add($"duplicate-event:{e.Id}");
        }

        if (e.Choices.IsDefaultOrEmpty)
        {
            violations.Add($"no-choices:{e.Id}");
        }

        if (e.Weight <= 0)
        {
            violations.Add($"weight-nonpositive:{e.Id}");
        }

        if (!HasValidWeeks(e))
        {
            violations.Add($"week-range:{e.Id}");
        }

        var choiceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var choice in ChoicesOf(e))
        {
            ValidateChoice(e.Id, choice, schema, choiceIds, violations);
        }
    }

    private static void ValidateChoice(string owner, ChoiceDefinition choice, int schema, HashSet<string> choiceIds, List<string> violations)
    {
        if (!choiceIds.Add(choice.Id))
        {
            violations.Add($"duplicate-choice:{owner}/{choice.Id}");
        }

        if (!choice.Guaranteed && (choice.BaseChance < Fixed.Zero || choice.BaseChance > Fixed.One))
        {
            violations.Add($"chance-range:{owner}/{choice.Id}");
        }

        if (!AllEffects(choice).All(effect => IsValidEffect(effect, schema)))
        {
            violations.Add($"effect-invalid:{owner}/{choice.Id}");
        }
    }

    private static ImmutableArray<ChoiceDefinition> ChoicesOf(EventDefinition e) => e.Choices.IsDefault ? [] : e.Choices;

    private static IEnumerable<Effect> AllEffects(ChoiceDefinition choice)
        => choice.Outcomes.Success
            .Concat(choice.Outcomes.Failure)
            .Concat(choice.Outcomes.CriticalSuccess ?? [])
            .Concat(choice.Outcomes.CriticalFailure ?? []);

    private static bool HasValidWeeks(EventDefinition e)
        => e.Eligibility.FromWeek >= CareerWeek.First
            && e.Eligibility.ToWeek <= CareerWeek.Last
            && e.Eligibility.FromWeek <= e.Eligibility.ToWeek;

    /// <summary>
    /// Checks one effect's shape: its amount range and that only the fields its kind uses are set. The M3 and M3-B kinds
    /// are valid only in schema 2, and a stat delta on followers is never valid.
    /// </summary>
    private static bool IsValidEffect(Effect effect, int schema) => effect.Kind switch
    {
        EffectKind.StatDelta => effect.Stat is { } stat && stat != StatKey.Followers && UsesOnly(effect) && Abs(effect.Amount) <= Fixed.Hundred,
        EffectKind.CashDelta => effect.Stat is null
            && UsesOnly(effect)
            && IsWhole(effect.Amount)
            && Abs(effect.Amount) <= Fixed.FromInt(MaxCashEffectPln),
        EffectKind.AudienceMultiplier => effect.Stat is null && UsesOnly(effect) && effect.Amount > Fixed.Zero && effect.Amount <= Fixed.FromInt(10),
        EffectKind.EventQuality => effect.Stat is null && UsesOnly(effect) && Abs(effect.Amount) <= Fixed.Hundred,
        _ => schema == 2 && effect.Stat is null && IsValidSchemaTwoEffect(effect),
    };

    private static bool IsValidSchemaTwoEffect(Effect effect) => effect.Kind switch
    {
        EffectKind.FollowersPercent => UsesOnly(effect) && Abs(effect.Amount) <= Fixed.FromInt(20),
        EffectKind.SkillDelta => effect.Category is not null && UsesOnly(effect, category: true) && Abs(effect.Amount) <= Fixed.FromInt(10),
        EffectKind.ViralChance => UsesOnly(effect) && effect.Amount >= Fixed.Zero && effect.Amount <= Fixed.FromInt(30),
        EffectKind.SetFlag => HasTarget(effect) && UsesOnly(effect, target: true, weeks: true) && effect.Weeks is null or >= 1,
        EffectKind.ClearFlag => HasTarget(effect) && UsesOnly(effect, target: true),
        EffectKind.FollowUp => HasTarget(effect) && UsesOnly(effect, target: true, weeks: true) && effect.Weeks is >= 1 and <= 52,
        EffectKind.PolicyStrike => UsesOnly(effect) && IsWhole(effect.Amount) && effect.Amount >= Fixed.One && effect.Amount <= Fixed.FromInt(2),
        EffectKind.GlobalBan => UsesOnly(effect) && effect.Amount == Fixed.Zero,
        EffectKind.SponsorOffer => HasTarget(effect) && UsesOnly(effect, target: true) && effect.Amount == Fixed.Zero,
        _ => false,
    };

    private static bool HasTarget(Effect effect) => !string.IsNullOrEmpty(effect.Target);

    /// <summary>True when every optional field that the kind does not use is null.</summary>
    private static bool UsesOnly(Effect effect, bool category = false, bool target = false, bool weeks = false)
        => (category || effect.Category is null) && (target || effect.Target is null) && (weeks || effect.Weeks is null);

    private static bool IsWhole(Fixed value) => value.Raw % Fixed.Scale == 0;

    private static Fixed Abs(Fixed value) => value < Fixed.Zero ? -value : value;

    /// <summary>Every week needs enough events by week range alone; reports the first week of each short run.</summary>
    private static void ValidateWeeklySupply(ContentSnapshot c, List<string> violations)
        => violations.AddRange(ShortRunStarts(c.Events.Where(HasValidWeeks), c.Balance.EventsPerWeekMax)
            .Select(week => $"too-few-eligible:week-{week}"));

    /// <summary>Every category needs enough live events by week range and category; reports the first week of each short run.</summary>
    private static void ValidateCategorySupply(ContentSnapshot c, List<string> violations)
    {
        var live = c.Events.Where(e => e.Phase == EventPhase.Live && HasValidWeeks(e)).ToArray();
        foreach (var category in Enum.GetValues<Category>())
        {
            var eligible = live.Where(e => e.Eligibility.Categories.IsEmpty || e.Eligibility.Categories.Contains(category));
            violations.AddRange(ShortRunStarts(eligible, c.Balance.EventsPerWeekMax)
                .Select(week => $"too-few-eligible:{category}:week-{week}"));
        }
    }

    /// <summary>The first week of each run of weeks in which fewer than <paramref name="needed"/> events are in range.</summary>
    private static IEnumerable<int> ShortRunStarts(IEnumerable<EventDefinition> events, int needed)
    {
        var ranges = events.Select(e => (e.Eligibility.FromWeek, e.Eligibility.ToWeek)).ToArray();
        var previousWeekShort = false;
        for (var week = CareerWeek.First; week <= CareerWeek.Last; week++)
        {
            var eligible = ranges.Count(r => r.FromWeek <= week && week <= r.ToWeek);
            var isShort = eligible < needed;
            if (isShort && !previousWeekShort)
            {
                yield return week;
            }

            previousWeekShort = isShort;
        }
    }

    private static void ValidateSchemaTwo(ContentSnapshot c, List<string> violations)
    {
        var ids = c.Events.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var eventChoices = c.Events.SelectMany(e => ChoicesOf(e)).ToArray();
        var allChoices = c.Balance.NetworkCollaborate is { } network ? eventChoices.Append(network) : eventChoices;
        var setFlags = TargetsOf(allChoices, EffectKind.SetFlag);
        var followUpTargets = TargetsOf(allChoices, EffectKind.FollowUp);

        foreach (var e in c.Events)
        {
            ValidateDecisionShape(e, violations);
            ValidateTexts(e.Id, Texts(e), violations);
            violations.AddRange(e.Eligibility.RequiresFlags
                .Where(flag => !setFlags.Contains(flag) && !EngineFlags.Contains(flag) && !flag.StartsWith(SystemFlagPrefix, StringComparison.Ordinal))
                .Select(flag => $"unset-flag:{e.Id}/{flag}"));
            ValidateChoiceLinks(e, c, ids, violations);
            ValidateGlobalBans(e, violations);

            var triggered = string.Equals(e.Id, c.Balance.RecoveryCrisisEventId, StringComparison.Ordinal) || followUpTargets.Contains(e.Id);
            if (e.Phase != EventPhase.Live && !HasValidWeeks(e) && !triggered)
            {
                violations.Add($"unreachable-decision:{e.Id}");
            }
        }

        if (c.Balance.NetworkCollaborate is { } networkChoice)
        {
            ValidateBalanceChoice(NetworkChoiceOwner, networkChoice, c, ids, violations);
        }

        if (c.Balance.Sponsors is { } sponsors)
        {
            ValidateBalanceChoice(PitchChoiceOwner, sponsors.Pitch, c, ids, violations);
        }

        if (c.Balance.RecoveryCrisisEventId is { } recovery
            && !c.Events.Any(e => e.Id == recovery && e.Phase == EventPhase.Crisis))
        {
            violations.Add($"missing-recovery-crisis:{recovery}");
        }

        ValidateCategorySupply(c, violations);
        ValidateSponsors(c, violations);
        ValidateEconomyBalance(c, violations);
    }

    private static void ValidateSponsors(ContentSnapshot c, List<string> violations)
    {
        var sponsorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in c.Sponsors)
        {
            AddUnless(sponsorIds.Add(t.Id), $"sponsor-duplicate:{t.Id}", violations);
            AddUnless(t.Version >= 1, $"sponsor-version:{t.Id}", violations);
            AddUnless(HasValidPayment(t), $"sponsor-payment:{t.Id}", violations);
            AddUnless(HasValidBreach(t), $"sponsor-breach:{t.Id}", violations);
            AddUnless(HasValidTerm(t), $"sponsor-term:{t.Id}", violations);
            AddUnless(HasPath(t), $"sponsor-no-path:{t.Id}", violations);
            violations.AddRange(t.IncompatibleWith
                .Where(value => !Incompatibilities.Contains(value))
                .Select(value => $"sponsor-incompatible:{t.Id}/{value}"));
            AddUnless(t.Weight > 0, $"sponsor-weight:{t.Id}", violations);
            ValidateTexts($"sponsor:{t.Id}", [("name", t.Name), ("pitch", t.Pitch)], violations);
        }
    }

    /// <summary>A positive base payment, a non-negative follower rate and a cap no lower than the base.</summary>
    private static bool HasValidPayment(SponsorTemplate t)
        => t.PaymentPln > 0 && t.PaymentPerThousandFollowersPln >= 0 && t.PaymentCapPln >= t.PaymentPln;

    /// <summary>A breach returns at most the payment cap and costs between 0 and 20 credibility.</summary>
    private static bool HasValidBreach(SponsorTemplate t)
        => t.Breach.CashPln >= 0
            && t.Breach.CashPln <= t.PaymentCapPln
            && t.Breach.Credibility >= Fixed.FromInt(-20)
            && t.Breach.Credibility <= Fixed.Zero;

    /// <summary>A contract runs a whole number of delivery periods; a one-off has neither a term nor a period.</summary>
    private static bool HasValidTerm(SponsorTemplate t) => t.Kind == SponsorKind.Contract
        ? t is { TermWeeks: > 0 and var term, DueEveryWeeks: > 0 and var due } && term % due == 0
        : t is { TermWeeks: null, DueEveryWeeks: null };

    /// <summary>Prerequisites some career can meet: stat bounds in [0, 100] and ordered, and no gambling sponsor that forbids gambling.</summary>
    private static bool HasPath(SponsorTemplate t)
    {
        var p = t.Prerequisites;
        return p.MinFollowers >= 0
            && new[] { p.MinCredibility, p.MaxDrama, p.MinDrama }.All(bound => bound is not { } value || IsStat(value))
            && (p is not { MinDrama: { } minDrama, MaxDrama: { } maxDrama } || minDrama <= maxDrama)
            && !(t.RequiredCategory == Category.Gambling && t.IncompatibleWith.Contains(SponsorTemplate.Gambling));
    }

    /// <summary>In [0, 100].</summary>
    private static bool IsStat(Fixed value) => value >= Fixed.Zero && value <= Fixed.Hundred;

    /// <summary>The M3-B balance sections; each is checked only when present.</summary>
    private static void ValidateEconomyBalance(ContentSnapshot c, List<string> violations)
    {
        var b = c.Balance;
        if (b.Platforms is { } platforms)
        {
            AddUnless(IsSafePlatforms(platforms), "balance:platforms", violations);
            AddUnless(b.PlatformBan is not null && b.Migration is not null, "balance:platform-ban-missing", violations);
        }

        if (b.Migration is { } migration)
        {
            AddUnless(IsSafeMigration(migration), "balance:migration", violations);
        }

        if (b.Policy is { } policy)
        {
            AddUnless(IsShare(policy.ChanceCap) && policy.StrikesToBan >= 1 && policy.StrikeWeeks >= 1, "balance:policy", violations);
        }

        if (b.Wagers is { } wagers)
        {
            AddUnless(IsSafeWagers(wagers), "balance:wagers", violations);
        }

        if (b.SideBusiness is { } side)
        {
            AddUnless(
                side.FromWeek >= CareerWeek.First && side.FromWeek <= CareerWeek.Last && IsStat(side.QualityPerWork) && IsStat(side.QualityDecay),
                "balance:side-business",
                violations);
        }

        if (b.Sponsors is { } sponsors)
        {
            ValidateSponsorBalance(sponsors, c, violations);
        }

        if (b.PlatformBan is { } ban)
        {
            ValidatePlatformBan(ban, violations);
        }
    }

    /// <summary>Every platform has positive factors and a non-negative policy risk.</summary>
    private static bool IsSafePlatforms(ImmutableDictionary<Platform, PlatformProfile> platforms)
        => Enum.GetValues<Platform>().All(platform => platforms.TryGetValue(platform, out var p)
            && p.Reach > Fixed.Zero
            && p.SubYield > Fixed.Zero
            && p.AdYield > Fixed.Zero
            && p.CategoryReach.Values.All(factor => factor > Fixed.Zero)
            && p.PolicyRisk >= Fixed.Zero);

    /// <summary>An ordered retention range in [0, 1], positive divisors and a non-negative jitter.</summary>
    private static bool IsSafeMigration(MigrationBalance m)
        => IsShare(m.RetentionMin)
            && IsShare(m.RetentionMax)
            && m.RetentionMin <= m.RetentionMax
            && m.CredibilityDivisor > Fixed.Zero
            && m.DramaDivisor > Fixed.Zero
            && m.Jitter >= Fixed.Zero;

    /// <summary>A positive stake and an exposure for every tier, a chance in [0, 1], a payout of at least the stake and a share in (0, 1].</summary>
    private static bool IsSafeWagers(WagerBalance w)
        => Enum.GetValues<WagerTier>().All(tier => w.StakePln.TryGetValue(tier, out var stake) && stake > 0 && w.Exposure.ContainsKey(tier))
            && IsShare(w.Chance)
            && w.PayoutMultiple >= Fixed.One
            && IsFraction(w.MaxShareOfCash);

    private static void ValidateSponsorBalance(SponsorBalance s, ContentSnapshot c, List<string> violations)
    {
        AddUnless(
            IsShare(s.OfferBase) && IsShare(s.OfferPerMagnitude) && IsShare(s.OfferCap) && s.OfferBase <= s.OfferCap
                && s.MaxContracts >= 1 && s.MissesToTerminate >= 1,
            "balance:sponsors",
            violations);
        AddUnless(
            !s.Pitch.AllowsCritical && AllEffects(s.Pitch).All(effect => effect.Kind == EffectKind.StatDelta),
            "balance:pitch",
            violations);
        foreach (var (background, id) in s.StartingLeads.OrderBy(lead => lead.Key))
        {
            ValidateLead(background, id, c, violations);
        }
    }

    /// <summary>A starting lead names a known template whose prerequisites the background's starting profile meets.</summary>
    private static void ValidateLead(Background background, string id, ContentSnapshot c, List<string> violations)
    {
        var template = c.Sponsors.FirstOrDefault(t => t.Id == id);
        if (template is null)
        {
            violations.Add($"missing-lead:{background}/{id}");
        }
        else if (c.Profiles.TryGetValue(background, out var profile) && !Meets(profile, template.Prerequisites))
        {
            violations.Add($"lead-unmet:{background}/{id}");
        }
    }

    /// <summary>Whether a career starting from <paramref name="profile"/> meets <paramref name="p"/> in week 1.</summary>
    private static bool Meets(StartingProfile profile, SponsorPrerequisites p)
        => profile.Followers >= p.MinFollowers
            && (p.MinCredibility is not { } minCredibility || profile.Credibility >= minCredibility)
            && (p.MaxDrama is not { } maxDrama || profile.Drama <= maxDrama)
            && (p.MinDrama is not { } minDrama || profile.Drama >= minDrama);

    /// <summary>ICU title, text and a move-choice label for every platform.</summary>
    private static void ValidatePlatformBan(PlatformBanText ban, List<string> violations)
    {
        AddUnless(Enum.GetValues<Platform>().All(ban.Choices.ContainsKey), "balance:platform-ban", violations);
        var labels = ban.Choices.OrderBy(choice => choice.Key).Select(choice => ($"choices.{choice.Key}", (string?)choice.Value));
        ValidateTexts("platformBan", new (string Field, string? Text)[] { ("title", ban.Title), ("text", ban.Text) }.Concat(labels), violations);
    }

    /// <summary>The network and pitch choices: ICU texts, links, and never a global ban.</summary>
    private static void ValidateBalanceChoice(string owner, ChoiceDefinition choice, ContentSnapshot c, HashSet<string> ids, List<string> violations)
    {
        ValidateTexts(owner, ChoiceTexts(choice), violations);
        ValidateFollowUps(owner, choice, c, ids, violations);
        ValidateSponsorOffers(owner, choice, c, violations);
        AddUnless(!HasGlobalBan(AllEffects(choice)), $"global-ban-placement:{owner}/{choice.Id}", violations);
    }

    private static HashSet<string> TargetsOf(IEnumerable<ChoiceDefinition> choices, EffectKind kind)
        => choices
            .SelectMany(AllEffects)
            .Where(effect => effect.Kind == kind && effect.Target is not null)
            .Select(effect => effect.Target!)
            .ToHashSet(StringComparer.Ordinal);

    private static void ValidateDecisionShape(EventDefinition e, List<string> violations)
    {
        if (e.Version < 1)
        {
            violations.Add($"version:{e.Id}");
        }

        if (e.Choices.IsDefaultOrEmpty || e.Choices.All(choice => choice.Requires is not null))
        {
            violations.Add($"no-legal-choice:{e.Id}");
        }
    }

    private static void ValidateChoiceLinks(EventDefinition e, ContentSnapshot c, HashSet<string> ids, List<string> violations)
    {
        foreach (var choice in ChoicesOf(e))
        {
            ValidateFollowUps(e.Id, choice, c, ids, violations);
            ValidateSponsorOffers(e.Id, choice, c, violations);
            if (e.Phase == EventPhase.Live)
            {
                continue;
            }

            if (AllEffects(choice).Any(effect => DecisionForbidden.Contains(effect.Kind)))
            {
                violations.Add($"effect-phase:{e.Id}/{choice.Id}");
            }

            if (choice.AllowsCritical)
            {
                violations.Add($"critical-in-decision:{e.Id}/{choice.Id}");
            }
        }
    }

    /// <summary>Every follow-up of <paramref name="choice"/> must name an existing, repeatable event.</summary>
    private static void ValidateFollowUps(string owner, ChoiceDefinition choice, ContentSnapshot c, HashSet<string> ids, List<string> violations)
    {
        var targets = AllEffects(choice)
            .Where(effect => effect.Kind == EffectKind.FollowUp && effect.Target is not null)
            .Select(effect => effect.Target!)
            .ToArray();
        violations.AddRange(targets.Where(target => !ids.Contains(target)).Select(target => $"broken-follow-up:{owner}/{target}"));
        violations.AddRange(targets
            .Where(target => c.Events.Any(x => x.Id == target && x.OneTime))
            .Select(target => $"follow-up-one-time:{owner}/{target}"));
    }

    /// <summary>Every sponsor offer of <paramref name="choice"/> must name a known sponsor template.</summary>
    private static void ValidateSponsorOffers(string owner, ChoiceDefinition choice, ContentSnapshot c, List<string> violations)
        => violations.AddRange(AllEffects(choice)
            .Where(effect => effect.Kind == EffectKind.SponsorOffer && effect.Target is not null)
            .Select(effect => effect.Target!)
            .Where(target => !c.Sponsors.Any(t => t.Id == target))
            .Select(target => $"unknown-sponsor:{owner}/{target}"));

    /// <summary>A global ban may only be a failure or critical-failure outcome of a crisis choice whose risk is rolled.</summary>
    private static void ValidateGlobalBans(EventDefinition e, List<string> violations)
        => violations.AddRange(ChoicesOf(e)
            .Where(choice => !HasSafeGlobalBans(e.Phase, choice))
            .Select(choice => $"global-ban-placement:{e.Id}/{choice.Id}"));

    private static bool HasSafeGlobalBans(EventPhase phase, ChoiceDefinition choice)
        => !HasGlobalBan(AllEffects(choice))
            || (phase == EventPhase.Crisis
                && !choice.Guaranteed
                && !HasGlobalBan(choice.Outcomes.Success)
                && !HasGlobalBan(choice.Outcomes.CriticalSuccess ?? []));

    private static bool HasGlobalBan(IEnumerable<Effect> effects) => effects.Any(effect => effect.Kind == EffectKind.GlobalBan);

    private static void ValidateTexts(string owner, IEnumerable<(string Field, string? Text)> texts, List<string> violations)
    {
        foreach (var (field, text) in texts)
        {
            if (text is null)
            {
                violations.Add($"text:{owner}/{field}:missing");
            }
            else if (IcuMessage.Validate(text) is { } problem)
            {
                violations.Add($"text:{owner}/{field}:{problem}");
            }
        }
    }

    private static IEnumerable<(string Field, string? Text)> Texts(EventDefinition e)
        => new (string Field, string? Text)[] { ("title", e.Title), ("text", e.TextPl) }
            .Concat(ChoicesOf(e).SelectMany(ChoiceTexts));

    private static IEnumerable<(string Field, string? Text)> ChoiceTexts(ChoiceDefinition choice)
    {
        yield return ($"{choice.Id}.label", choice.LabelPl);
        if (choice.SuccessText is { } success)
        {
            yield return ($"{choice.Id}.success", success);
        }

        if (choice.FailureText is { } failure)
        {
            yield return ($"{choice.Id}.failure", failure);
        }
    }
}
