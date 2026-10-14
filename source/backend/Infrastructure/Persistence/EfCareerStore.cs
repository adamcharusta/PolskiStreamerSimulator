using System.Text;
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server <see cref="ICareerStore"/>: a state snapshot per career and an append-only command log.</summary>
public sealed class EfCareerStore(IDbContextFactory<GameDbContext> contexts) : ICareerStore
{
    public async Task CreateAsync(NewCareer career, CancellationToken cancellationToken)
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
        });
        db.CareerSecrets.Add(new CareerSecretRow { CareerId = career.Id, SecretHash = career.SecretHash, CreatedAt = career.CreatedAt });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.Careers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == careerId, cancellationToken);
        return row is null
            ? null
            : new StoredCareer(row.Id, row.Nickname, CanonicalJson.Deserialize(Encoding.UTF8.GetBytes(row.StateJson)), row.CreatedAt, row.LastActionAt);
    }

    public async Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.CareerSecrets.AsNoTracking()
            .Where(s => s.CareerId == careerId)
            .Select(s => s.SecretHash)
            .SingleOrDefaultAsync(cancellationToken);
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

        // The version condition both detects a concurrent advance and locks the row until commit.
        var updated = await db.Careers
            .Where(c => c.Id == careerId && c.StateVersion == expectedVersion)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(c => c.StateVersion, newState.StateVersion)
                    .SetProperty(c => c.StateJson, json)
                    .SetProperty(c => c.StateHash, hash)
                    .SetProperty(c => c.Status, newState.Status)
                    .SetProperty(c => c.LastActionAt, at),
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
