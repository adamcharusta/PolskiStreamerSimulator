using System.Text;
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server <see cref="ICareerStore"/>: a state snapshot per career and an append-only command log.</summary>
public sealed class EfCareerStore(IDbContextFactory<GameDbContext> contexts) : ICareerStore
{
    public async Task<CreateOutcome> CreateAsync(NewCareer career, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        db.Careers.Add(new CareerRow
        {
            Id = career.Id,
            Nickname = career.Nickname,
            RulesetId = career.State.RulesetId,
            ContentDigest = career.State.ContentDigest,
            Ranked = false,
            Status = career.State.Status,
            StateVersion = career.State.StateVersion,
            StateJson = JsonOf(career.State),
            StateHash = StateHash.Of(career.State),
            CreatedAt = career.CreatedAt,
            LastActionAt = career.CreatedAt,
            ExpiresAt = CareerRetention.ExpiresAt(career.State, career.CreatedAt),
        });
        db.CareerSecrets.Add(new CareerSecretRow
        {
            CareerId = career.Id,
            SecretHash = career.SecretHash,
            RecoveryHash = career.RecoveryHash,
            CreationHash = career.Creation?.CreationHash,
            CreationRequestHash = career.Creation?.RequestHash,
            CreatedAt = career.CreatedAt,
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return CreateOutcome.Created;
        }
        catch (DbUpdateException error) when (career.Creation is not null && SqlErrors.IsDuplicateKey(error))
        {
            return CreateOutcome.DuplicateCreation;
        }
    }

    public async Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.Careers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == careerId, cancellationToken);
        return row is null
            ? null
            : new StoredCareer(
                row.Id,
                row.Nickname,
                CanonicalJson.Deserialize(Encoding.UTF8.GetBytes(row.StateJson)),
                row.CreatedAt,
                row.LastActionAt,
                row.ExpiresAt,
                row.EndedAt);
    }

    public async Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.CareerSecrets.AsNoTracking()
            .Where(s => s.CareerId == careerId)
            .Select(s => s.SecretHash)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CareerCredentials?> FindCredentialsAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.CareerSecrets.AsNoTracking()
            .Where(s => s.CareerId == careerId)
            .Select(s => new CareerCredentials(s.SecretHash, s.RecoveryHash, s.CreationRequestHash))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> FindByCreationAsync(byte[] creationHash, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.CareerSecrets.AsNoTracking()
            .Where(s => s.CreationHash == creationHash)
            .Select(s => (Guid?)s.CareerId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ReplaceCredentialsAsync(
        Guid careerId,
        byte[]? secretHash,
        byte[]? recoveryHash,
        long? whileVersion,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var updated = await db.CareerSecrets
            .Where(s => s.CareerId == careerId
                && (whileVersion == null || db.Careers.Any(c => c.Id == careerId && c.StateVersion == whileVersion)))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(s => s.SecretHash, s => secretHash ?? s.SecretHash)
                    .SetProperty(s => s.RecoveryHash, s => recoveryHash ?? s.RecoveryHash)
                    .SetProperty(s => s.RotatedAt, at),
                cancellationToken);
        return updated == 1;
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        // The foreign keys cascade to career_secrets and career_actions in the same statement.
        return await db.Careers
            .Where(c => c.ExpiresAt <= now)
            .OrderBy(c => c.ExpiresAt)
            .Take(limit)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.CareerActions.AsNoTracking()
            .Where(a => a.CareerId == careerId && a.CommandId == commandId)
            .OrderBy(a => a.Step)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(ToStored)];
    }

    public async Task<IReadOnlyList<StoredAction>> ListReportsAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.CareerActions.AsNoTracking()
            .Where(a => a.CareerId == careerId && a.ReportJson != null)
            .OrderBy(a => a.Sequence)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(ToStored)];
    }

    public async Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.CareerActions.AsNoTracking()
            .Where(a => a.CareerId == careerId && a.ReportJson != null)
            .OrderByDescending(a => a.Sequence)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToStored(row);
    }

    public async Task<AppendOutcome> AppendAsync(
        Guid careerId,
        long expectedVersion,
        CareerState newState,
        NewAction action,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var json = JsonOf(newState);
        var hash = StateHash.Of(newState);
        DateTimeOffset? endedAt = newState.Status == CareerStatus.Ended ? at : null;

        // The version condition both detects a concurrent advance and locks the row until commit.
        var updated = await db.Careers
            .Where(c => c.Id == careerId && c.StateVersion == expectedVersion)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(c => c.StateVersion, newState.StateVersion)
                    .SetProperty(c => c.StateJson, json)
                    .SetProperty(c => c.StateHash, hash)
                    .SetProperty(c => c.Status, newState.Status)
                    .SetProperty(c => c.LastActionAt, at)
                    .SetProperty(c => c.ExpiresAt, CareerRetention.ExpiresAt(newState, at))
                    .SetProperty(c => c.EndedAt, c => endedAt ?? c.EndedAt),
                cancellationToken);
        if (updated == 0)
        {
            return AppendOutcome.VersionConflict;
        }

        var last = await db.CareerActions.Where(a => a.CareerId == careerId).MaxAsync(a => (int?)a.Sequence, cancellationToken) ?? 0;
        db.CareerActions.Add(new CareerActionRow
        {
            CareerId = careerId,
            Sequence = last + 1,
            CommandId = action.CommandId,
            Step = action.Step,
            Kind = action.Kind,
            RequestJson = action.RequestJson,
            ResponseJson = action.ResponseJson,
            ReportJson = action.ReportJson,
            StateVersionAfter = newState.StateVersion,
            StateHashAfter = hash,
            CreatedAt = at,
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (SqlErrors.IsDuplicateKey(error))
        {
            return AppendOutcome.DuplicateCommand;
        }

        await transaction.CommitAsync(cancellationToken);
        return AppendOutcome.Appended;
    }

    private static string JsonOf(CareerState state) => Encoding.UTF8.GetString(CanonicalJson.Serialize(state));

    private static StoredAction ToStored(CareerActionRow row)
        => new(row.Sequence, row.CommandId, row.Step, row.Kind, row.RequestJson, row.ResponseJson, row.ReportJson, row.StateVersionAfter, row.StateHashAfter, row.CreatedAt);
}
