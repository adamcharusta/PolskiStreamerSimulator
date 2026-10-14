using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Application.Careers;

public static class CareerReads
{
    /// <summary>The career's pinned content; missing content is a server fault.</summary>
    public static async Task<ContentSnapshot> RequireAsync(this IContentStore contents, string digest, CancellationToken cancellationToken)
        => await contents.FindAsync(digest, cancellationToken)
            ?? throw new InvalidOperationException($"Content snapshot {digest} is not stored.");

    /// <summary>The newest stored week report, as the client last saw it.</summary>
    public static async Task<WeekReportView?> LastReportAsync(this ICareerStore store, Guid careerId, CancellationToken cancellationToken)
        => await store.FindLatestReportAsync(careerId, cancellationToken) is { ReportJson: { } json }
            ? ContractJson.Deserialize<WeekReportView>(json)
            : null;
}
