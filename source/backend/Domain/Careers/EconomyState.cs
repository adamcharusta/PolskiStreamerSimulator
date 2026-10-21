using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Careers;

/// <summary>M3-B: a policy strike given by a platform in <see cref="GivenWeek"/>.</summary>
public sealed record PolicyStrike(Platform Platform, int GivenWeek);

/// <summary>M3-B: a sponsor offer that can be accepted until <see cref="ExpiresAfterWeek"/>.</summary>
public sealed record SponsorOffer(string OfferId, string TemplateId, int OfferedWeek, int ExpiresAfterWeek, long PaymentPln);

/// <summary>M3-B: an accepted sponsor deal; <see cref="Missed"/> counts the breached deliveries so far.</summary>
public sealed record SponsorContract(
    string ContractId,
    string TemplateId,
    SponsorKind Kind,
    int AcceptedWeek,
    int NextDueWeek,
    int TermEndWeek,
    long PaymentPln,
    int Missed);

/// <summary>M3-B: the text-only side business with its quality (0 to 100).</summary>
public sealed record SideBusiness(int OpenedWeek, Fixed Quality);

/// <summary>M3-B: a move between platforms and the share of followers that follows.</summary>
public sealed record Migration(Platform From, Platform To, Fixed Retention, bool Forced);

/// <summary>M3-B helpers that read the optional economy fields with their defaults.</summary>
public static class Economy
{
    public static Platform Current(CareerState s) => s.Platform ?? Platform.Czatnia;

    public static int SinceWeek(CareerState s) => s.PlatformSinceWeek ?? CareerWeek.First;

    public static bool IsBanned(CareerState s, Platform p) => s.BannedPlatforms is { } banned && banned.Contains(p);

    /// <summary>Strikes on <paramref name="platform"/> still active after <paramref name="settledWeek"/>: given within the last <paramref name="strikeWeeks"/> weeks.</summary>
    public static int ActiveStrikes(CareerState s, Platform platform, int settledWeek, int strikeWeeks)
        => s.Strikes is { } strikes ? strikes.Count(x => x.Platform == platform && x.GivenWeek + strikeWeeks > settledWeek) : 0;
}
