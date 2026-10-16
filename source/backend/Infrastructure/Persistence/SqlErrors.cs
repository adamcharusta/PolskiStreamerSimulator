using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

internal static class SqlErrors
{
    /// <summary>SQL Server 2601/2627: a unique index or primary key rejected the insert.</summary>
    public static bool IsDuplicateKey(DbUpdateException error) => error.InnerException is SqlException { Number: 2601 or 2627 };
}
