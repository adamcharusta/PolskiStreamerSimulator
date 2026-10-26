using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Thread-safe <see cref="ICareerStore"/> with the same rules as the SQL Server store.</summary>
public sealed class InMemoryCareerStore : ICareerStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<Guid, StoredCareer> careers = [];
    private readonly Dictionary<Guid, byte[]> secrets = [];
    private readonly Dictionary<Guid, List<StoredAction>> logs = [];

    public Task CreateAsync(NewCareer career, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            careers.Add(career.Id, new StoredCareer(career.Id, career.Nickname, career.State, career.CreatedAt, career.CreatedAt));
            secrets.Add(career.Id, career.SecretHash);
            logs.Add(career.Id, []);
        }

        return Task.CompletedTask;
    }

    public Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(careers.GetValueOrDefault(careerId));
        }
    }

    public Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(secrets.GetValueOrDefault(careerId));
        }
    }

    public Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            IReadOnlyList<StoredAction> steps = logs.TryGetValue(careerId, out var log)
                ? [.. log.Where(a => a.CommandId == commandId).OrderBy(a => a.Step)]
                : [];
            return Task.FromResult(steps);
        }
    }

    public Task<IReadOnlyList<StoredAction>> ListReportsAsync(Guid careerId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            IReadOnlyList<StoredAction> reports = logs.TryGetValue(careerId, out var log) ? [.. log.Where(a => a.ReportJson is not null)] : [];
            return Task.FromResult(reports);
        }
    }

    public Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(logs.TryGetValue(careerId, out var log) ? log.LastOrDefault(a => a.ReportJson is not null) : null);
        }
    }

    public Task<AppendOutcome> AppendAsync(
        Guid careerId,
        long expectedVersion,
        CareerState newState,
        NewAction action,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var career = careers[careerId];
            if (career.State.StateVersion != expectedVersion)
            {
                return Task.FromResult(AppendOutcome.VersionConflict);
            }

            var log = logs[careerId];
            if (log.Any(a => a.CommandId == action.CommandId && a.Step == action.Step))
            {
                return Task.FromResult(AppendOutcome.DuplicateCommand);
            }

            log.Add(new StoredAction(
                log.Count + 1,
                action.CommandId,
                action.Step,
                action.Kind,
                action.RequestJson,
                action.ResponseJson,
                action.ReportJson,
                newState.StateVersion,
                StateHash.Of(newState),
                at));
            careers[careerId] = career with { State = newState, LastActionAt = at };
            return Task.FromResult(AppendOutcome.Appended);
        }
    }

    /// <summary>Number of logged steps for <paramref name="careerId"/>.</summary>
    public int LogCount(Guid careerId)
    {
        lock (gate)
        {
            return logs.TryGetValue(careerId, out var log) ? log.Count : 0;
        }
    }
}
