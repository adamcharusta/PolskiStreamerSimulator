using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>
/// M3-B content for economy rule tests: <see cref="M3TestContent"/> for <c>m3-systems.2</c> with the seven M3-B
/// balance sections (the proposed values), the Industry Insider's starting lead and four sponsor templates. Built in
/// code so rule tests do not depend on authored Polish content.
/// </summary>
public static class M3BTestContent
{
    /// <summary>A trusted, low-drama contract: 300 PLN + 20 per thousand followers (cap 1,000), 12 weeks, due every 4.</summary>
    public static readonly SponsorTemplate TrustedTea = new(
        "trusted-tea",
        1,
        "Herbata Spokój",
        "Spokojna marka szuka spokojnego kanału.",
        SponsorKind.Contract,
        300,
        20,
        1000,
        12,
        4,
        null,
        [],
        new SponsorPrerequisites(1000, Fixed.FromInt(40), Fixed.FromInt(40), null),
        [SponsorTemplate.Gambling],
        new SponsorBreach(200, Fixed.FromInt(-3)),
        3,
        []);

    /// <summary>A sensational one-off deal for channels with drama of at least 50.</summary>
    public static readonly SponsorTemplate HypeDrink = new(
        "hype-drink",
        1,
        "Napój Szał",
        "Głośna marka chce głośnego kanału.",
        SponsorKind.OneOff,
        400,
        0,
        400,
        null,
        null,
        null,
        [],
        new SponsorPrerequisites(0, null, null, Fixed.FromInt(50)),
        [],
        new SponsorBreach(100, Fixed.FromInt(-2)),
        2,
        []);

    /// <summary>A one-off deal that needs a Chess stream and stays away from the side business.</summary>
    public static readonly SponsorTemplate ChessBoard = new(
        "chess-board",
        1,
        "Szachownica Mistrz",
        "Producent szachownic szuka partii na żywo.",
        SponsorKind.OneOff,
        250,
        0,
        250,
        null,
        null,
        Category.Chess,
        [],
        new SponsorPrerequisites(0, null, null, null),
        [SponsorTemplate.AdultBusiness],
        new SponsorBreach(100, Fixed.FromInt(-1)),
        1,
        []);

    /// <summary>A PulsLive-only contract for channels with at least 5,000 followers: 500 PLN, 8 weeks, due every 4.</summary>
    public static readonly SponsorTemplate PulsGear = new(
        "puls-gear",
        1,
        "Sprzęt Puls",
        "Sklep ze sprzętem płaci za widoczność na PulsLive.",
        SponsorKind.Contract,
        500,
        0,
        800,
        8,
        4,
        null,
        [Platform.PulsLive],
        new SponsorPrerequisites(5000, null, null, null),
        [],
        new SponsorBreach(300, Fixed.FromInt(-4)),
        1,
        []);

    /// <summary>The pitch choice: base 15 %, +0.4 points per credibility point (35 % at 50), no critical.</summary>
    public static readonly ChoiceDefinition Pitch = new(
        "pitch",
        null,
        "Rozpisz ofertę dla sponsorów",
        false,
        Fixed.FromRatio(15, 100),
        [new ChanceModifier(StatKey.Credibility, Fixed.FromRatio(4, 1000), null, "modifier.credibility")],
        false,
        new OutcomeSet([new Effect(EffectKind.StatDelta, StatKey.Credibility, Fixed.One)], [], null, null),
        SuccessText: "Ktoś odpisał.",
        FailureText: "Cisza w skrzynce.");

    private static readonly Lazy<ContentSnapshot> Snapshot = new(Build);

    public static ContentSnapshot Valid() => Snapshot.Value;

    private static ContentSnapshot Build()
    {
        var m3 = M3TestContent.Valid();
        static Fixed H(int hundredths) => Fixed.FromRatio(hundredths, 100);
        var neutralReach = CategoryValues<Fixed>.Uniform(Fixed.One);
        return m3 with
        {
            RulesetId = M3Ruleset.SystemsV2,
            Digest = "m3b-test",
            Sponsors = [TrustedTea, HypeDrink, ChessBoard, PulsGear],
            Balance = m3.Balance with
            {
                Platforms = new Dictionary<Platform, PlatformProfile>
                {
                    [Platform.Czatnia] = new(Fixed.One, Fixed.One, Fixed.One, Fixed.One, neutralReach),
                    [Platform.PulsLive] = new(
                        H(120), H(85), H(110), H(130), neutralReach.With(Category.Fps, H(110)).With(Category.Moba, H(110)).With(Category.Chess, H(90))),
                    [Platform.Nurt] = new(
                        H(85), H(125), H(70), H(80), neutralReach.With(Category.Chess, H(120)).With(Category.JustChatting, H(110)).With(Category.Gambling, H(80))),
                }.ToImmutableDictionary(),
                Migration = new MigrationBalance(
                    Fixed.FromInt(6), 26, 4, Fixed.FromInt(-1), H(10), H(10), H(40), Fixed.FromInt(250), Fixed.FromInt(500), H(20), H(80), H(5)),
                Policy = new PolicyBalance(1, 1, 1, Fixed.FromInt(70), Fixed.FromRatio(15, 1000), H(15), 26, 3, Fixed.One),
                Sponsors = new SponsorBalance(
                    H(8), H(2), H(20), 2, 2, Fixed.FromInt(8), Pitch, Fixed.FromInt(-5), 2,
                    ImmutableDictionary<Background, string>.Empty.Add(Background.IndustryInsider, TrustedTea.Id)),
                Wagers = new WagerBalance(
                    Tiers(100L, 500L, 2000L), Tiers(1, 2, 3), H(25), H(45), Fixed.FromInt(2), H(115), H(105), Fixed.FromInt(2), Fixed.FromInt(3)),
                SideBusiness = new SideBusinessBalance(
                    52, 2000, Fixed.FromInt(12), Fixed.FromInt(4), Fixed.FromInt(15), Fixed.FromInt(3), Fixed.FromRatio(5, 10_000), Fixed.FromInt(10), Fixed.FromRatio(-2, 10)),
                PlatformBan = new PlatformBanText(
                    "Ban na platformie",
                    "Platforma zamyka ci kanał na stałe. Dokąd przenosisz społeczność?",
                    new Dictionary<Platform, string>
                    {
                        [Platform.Czatnia] = "Przenieś się na Czatnię",
                        [Platform.PulsLive] = "Przenieś się na PulsLive",
                        [Platform.Nurt] = "Przenieś się na Nurt",
                    }.ToImmutableDictionary()),
            },
        };
    }

    private static ImmutableDictionary<WagerTier, T> Tiers<T>(T low, T medium, T high)
        => new Dictionary<WagerTier, T> { [WagerTier.Low] = low, [WagerTier.Medium] = medium, [WagerTier.High] = high }.ToImmutableDictionary();
}
