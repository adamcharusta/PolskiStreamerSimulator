using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>What a career needs to accept a sponsor; null bounds and zero followers do not limit.</summary>
public sealed record SponsorPrerequisites(long MinFollowers, Fixed? MinCredibility, Fixed? MaxDrama, Fixed? MinDrama);

/// <summary>The penalty of a breached delivery: cash returned and a credibility change.</summary>
public sealed record SponsorBreach(long CashPln, Fixed Credibility);

/// <summary>
/// M3-B: a fictional sponsor that can make offers. The payment is <see cref="PaymentPln"/> plus
/// <see cref="PaymentPerThousandFollowersPln"/> per thousand followers, capped at <see cref="PaymentCapPln"/>. A contract
/// runs <see cref="TermWeeks"/> weeks with a delivery every <see cref="DueEveryWeeks"/> weeks; a one-off sets neither.
/// Empty <see cref="Platforms"/> means every platform. Like events, a template never changes under the same version.
/// </summary>
public sealed record SponsorTemplate(
    string Id,
    int Version,
    string Name,
    string Pitch,
    SponsorKind Kind,
    long PaymentPln,
    long PaymentPerThousandFollowersPln,
    long PaymentCapPln,
    int? TermWeeks,
    int? DueEveryWeeks,
    Category? RequiredCategory,
    ImmutableArray<Platform> Platforms,
    SponsorPrerequisites Prerequisites,
    ImmutableArray<string> IncompatibleWith,
    SponsorBreach Breach,
    int Weight,
    ImmutableArray<string> Tags)
{
    /// <summary>Incompatibility with gambling streams and gambling sponsors.</summary>
    public const string Gambling = "gambling";

    /// <summary>Incompatibility with the (adult-content) side business.</summary>
    public const string AdultBusiness = "adult-business";

    public ImmutableArray<Platform> Platforms { get; init; } = Platforms.IsDefault ? [] : Platforms;

    public ImmutableArray<string> IncompatibleWith { get; init; } = IncompatibleWith.IsDefault ? [] : IncompatibleWith;

    public ImmutableArray<string> Tags { get; init; } = Tags.IsDefault ? [] : Tags;
}
