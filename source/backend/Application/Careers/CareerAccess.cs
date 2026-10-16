using PolskiStreamerSimulator.Application.Careers.Persistence;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Opens a career only with its own secret; an unknown career and a wrong secret look the same.</summary>
public sealed class CareerAccess(ICareerStore store, ISecretHasher secrets)
{
    public async Task<AppResult<StoredCareer>> OpenAsync(Guid careerId, string? secret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return AppResult<StoredCareer>.Fail(ErrorCodes.MissingSecret);
        }

        var hash = await store.FindSecretHashAsync(careerId, cancellationToken);
        if (hash is null || !secrets.Matches(secret, hash))
        {
            return AppResult<StoredCareer>.Fail(ErrorCodes.CareerNotFound);
        }

        var career = await store.FindAsync(careerId, cancellationToken);
        return career is null ? AppResult<StoredCareer>.Fail(ErrorCodes.CareerNotFound) : AppResult<StoredCareer>.Ok(career);
    }
}
