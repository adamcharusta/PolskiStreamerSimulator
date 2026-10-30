using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Telemetry;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server daily counters; one MERGE per counter, so concurrent batches never lose an increment.</summary>
public sealed class EfTelemetryStore(IDbContextFactory<GameDbContext> contexts) : ITelemetryStore
{
    public async Task AddAsync(DateOnly day, IReadOnlyList<(string Event, string PropsKey)> counts, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var group in counts.GroupBy(c => c))
        {
            var (name, props) = group.Key;
            var add = group.Count();
            await db.Database.ExecuteSqlAsync(
                $"""
                MERGE [game].[telemetry_daily] WITH (HOLDLOCK) AS t
                USING (SELECT {day} AS [day], {name} AS [event], {props} AS [props_key]) AS s
                ON t.[day] = s.[day] AND t.[event] = s.[event] AND t.[props_key] = s.[props_key]
                WHEN MATCHED THEN UPDATE SET t.[count] = t.[count] + {add}
                WHEN NOT MATCHED THEN INSERT ([day], [event], [props_key], [count]) VALUES (s.[day], s.[event], s.[props_key], {add});
                """,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
