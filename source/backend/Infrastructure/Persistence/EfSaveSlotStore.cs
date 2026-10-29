using System.Text;
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server <see cref="ISaveSlotStore"/>; the career foreign key deletes slots with their career.</summary>
public sealed class EfSaveSlotStore(IDbContextFactory<GameDbContext> contexts) : ISaveSlotStore
{
    public async Task<IReadOnlyList<SaveSlot>> ListAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.SaveSlots.AsNoTracking().Where(s => s.CareerId == careerId).OrderBy(s => s.Slot).ToListAsync(cancellationToken);
        return [.. rows.Select(ToSlot)];
    }

    public async Task<SaveSlot?> FindAsync(Guid careerId, int number, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.SaveSlots.AsNoTracking().SingleOrDefaultAsync(s => s.CareerId == careerId && s.Slot == number, cancellationToken);
        return row is null ? null : ToSlot(row);
    }

    public async Task SaveAsync(Guid careerId, int number, CareerState state, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        // Versions never repeat in a log (a restore moves to the next one), so the version finds the entry that made it.
        var sequence = await db.CareerActions
            .Where(a => a.CareerId == careerId && a.StateVersionAfter == state.StateVersion)
            .MaxAsync(a => (int?)a.Sequence, cancellationToken) ?? 0;
        var json = Encoding.UTF8.GetString(CanonicalJson.Serialize(state));
        if (await OverwriteAsync(db, careerId, number, json, sequence, at, cancellationToken))
        {
            return;
        }

        db.SaveSlots.Add(new SaveSlotRow { CareerId = careerId, Slot = number, StateJson = json, LogSequence = sequence, SavedAt = at });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (SqlErrors.IsDuplicateKey(error))
        {
            // A concurrent save filled the slot first; this later save overwrites it, as it would have anyway.
            await OverwriteAsync(db, careerId, number, json, sequence, at, cancellationToken);
        }
    }

    private static async Task<bool> OverwriteAsync(GameDbContext db, Guid careerId, int number, string json, int sequence, DateTimeOffset at, CancellationToken cancellationToken)
        => await db.SaveSlots
            .Where(s => s.CareerId == careerId && s.Slot == number)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(s => s.StateJson, json)
                    .SetProperty(s => s.LogSequence, sequence)
                    .SetProperty(s => s.SavedAt, at),
                cancellationToken) > 0;

    private static SaveSlot ToSlot(SaveSlotRow row)
        => new(row.Slot, CanonicalJson.Deserialize(Encoding.UTF8.GetBytes(row.StateJson)), row.LogSequence, row.SavedAt);
}
