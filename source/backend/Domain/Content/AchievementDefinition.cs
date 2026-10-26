using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>What an M3-C achievement checks at a settlement or at retirement.</summary>
public enum AchievementKind
{
    /// <summary>Followers at least the threshold.</summary>
    Followers,

    /// <summary>Cash at least the threshold PLN.</summary>
    Cash,

    /// <summary>Week 1,040 settled.</summary>
    Completed,

    /// <summary>Skill at least the threshold in any category.</summary>
    Mastery,

    /// <summary>Weeks streamed in one category at least the threshold.</summary>
    Specialist,

    /// <summary>A platform move by choice: the career moved and no platform has banned it.</summary>
    PlatformMove,

    /// <summary>Paid sponsor installments at least the threshold.</summary>
    SponsorInstallments,

    /// <summary>The career completed after drama peaked at the threshold or above and then fell to 30 or below.</summary>
    DramaSurvived,
}

/// <summary>
/// M3-C: one achievement of the <c>balance.achievements</c> section, with its Polish ICU name and description and the
/// points it adds to the score's achievement component.
/// </summary>
public sealed record AchievementDefinition(string Id, string Name, string Description, AchievementKind Kind, Fixed? Threshold, int Points)
{
    /// <summary>True for the kinds that need a threshold.</summary>
    public static bool NeedsThreshold(AchievementKind kind) => kind is not (AchievementKind.Completed or AchievementKind.PlatformMove);
}
