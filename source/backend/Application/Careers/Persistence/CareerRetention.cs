using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers.Persistence;

/// <summary>
/// When the retention sweep deletes a career (technical architecture §4): six calendar months after the last
/// successful gameplay action while unfinished, 12 calendar months after the ending once ended.
/// </summary>
public static class CareerRetention
{
    public const int UnfinishedMonths = 6;

    public const int FinishedMonths = 12;

    /// <summary>The expiry date of a career whose last committed write at <paramref name="at"/> left it in <paramref name="state"/>.</summary>
    public static DateTimeOffset ExpiresAt(CareerState state, DateTimeOffset at)
        => at.ToUniversalTime().AddMonths(state.Status == CareerStatus.Ended ? FinishedMonths : UnfinishedMonths);
}
