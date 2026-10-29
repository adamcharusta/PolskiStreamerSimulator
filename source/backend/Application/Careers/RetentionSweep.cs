using PolskiStreamerSimulator.Application.Careers.Persistence;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Deletes every expired career with its secrets and log, in batches (spec M4-A §6).</summary>
public sealed class RetentionSweep(ICareerStore store, TimeProvider time)
{
    public const int BatchSize = 200;

    /// <returns>How many careers were deleted.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var total = 0;
        int deleted;
        do
        {
            deleted = await store.DeleteExpiredAsync(now, BatchSize, cancellationToken);
            total += deleted;
        }
        while (deleted == BatchSize);

        return total;
    }
}
