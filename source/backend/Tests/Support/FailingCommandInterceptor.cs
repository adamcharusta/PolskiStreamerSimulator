using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Throws before executing any command whose SQL contains <paramref name="marker"/> while armed.</summary>
public sealed class FailingCommandInterceptor(string marker) : DbCommandInterceptor
{
    public bool Armed { get; set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Fail(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Fail(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Fail(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Fail(DbCommand command)
    {
        if (Armed && command.CommandText.Contains(marker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Injected failure before: {marker}");
        }
    }
}
