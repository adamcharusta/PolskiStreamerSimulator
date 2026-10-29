using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Thread-safe <see cref="ICareerStore"/> with the same rules as the SQL Server store.</summary>
public sealed class InMemoryCareerStore : ICareerStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<Guid, StoredCareer> careers = [];
    private readonly Dictionary<Guid, CareerCredentials> secrets = [];
    private readonly Dictionary<string, Guid> creations = [];
    private readonly Dictionary<Guid, List<StoredAction>> logs = [];

    public Task<CreateOutcome> CreateAsync(NewCareer career, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var creation = career.Creation is null ? null : Convert.ToHexString(career.Creation.CreationHash);
            if (creation is not null && creations.ContainsKey(creation))
            {
                return Task.FromResult(CreateOutcome.DuplicateCreation);
            }

            careers.Add(career.Id, new StoredCareer(career.Id, career.Nickname, career.State, career.CreatedAt, career.CreatedAt, CareerRetention.ExpiresAt(career.State, career.CreatedAt), Season: career.Season));
            secrets.Add(career.Id, new CareerCredentials(career.SecretHash, career.RecoveryHash, career.Creation?.RequestHash));
            if (creation is not null)
            {
                creations.Add(creation, career.Id);
            }

            logs.Add(career.Id, []);
        }

        return Task.FromResult(CreateOutcome.Created);
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
            return Task.FromResult(secrets.GetValueOrDefault(careerId)?.SecretHash);
        }
    }

    public Task<CareerCredentials?> FindCredentialsAsync(Guid careerId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(secrets.GetValueOrDefault(careerId));
        }
    }

    public Task<Guid?> FindByCreationAsync(byte[] creationHash, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(creations.TryGetValue(Convert.ToHexString(creationHash), out var id) ? id : (Guid?)null);
        }
    }

    public Task<bool> ReplaceCredentialsAsync(
        Guid careerId,
        byte[]? secretHash,
        byte[]? recoveryHash,
        long? whileVersion,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (!careers.TryGetValue(careerId, out var career) || (whileVersion is { } version && career.State.StateVersion != version))
            {
                return Task.FromResult(false);
            }

            var current = secrets[careerId];
            secrets[careerId] = current with { SecretHash = secretHash ?? current.SecretHash, RecoveryHash = recoveryHash ?? current.RecoveryHash };
            return Task.FromResult(true);
        }
    }

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var expired = careers.Values.Where(c => c.ExpiresAt <= now).OrderBy(c => c.ExpiresAt).Take(limit).Select(c => c.Id).ToList();
            foreach (var id in expired)
            {
                careers.Remove(id);
                secrets.Remove(id);
                logs.Remove(id);
                foreach (var creation in creations.Where(c => c.Value == id).Select(c => c.Key).ToList())
                {
                    creations.Remove(creation);
                }
            }

            return Task.FromResult(expired.Count);
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
            careers[careerId] = career with
            {
                State = newState,
                LastActionAt = at,
                ExpiresAt = CareerRetention.ExpiresAt(newState, at),
                EndedAt = newState.Status == CareerStatus.Ended ? at : null,
            };
            return Task.FromResult(AppendOutcome.Appended);
        }
    }

    /// <summary>Removes the recovery hash, as for a career created before M4-A.</summary>
    public void ClearRecovery(Guid careerId)
    {
        lock (gate)
        {
            secrets[careerId] = secrets[careerId] with { RecoveryHash = null };
        }
    }

    /// <summary>Number of stored careers.</summary>
    public int Count
    {
        get
        {
            lock (gate)
            {
                return careers.Count;
            }
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
