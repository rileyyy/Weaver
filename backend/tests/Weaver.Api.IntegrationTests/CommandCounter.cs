using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Weaver.Api.IntegrationTests;

/// <summary>Counts the queries EF sends, to show a read doesn't grow a query per item.</summary>
public class CommandCounter : DbCommandInterceptor
{
    public int Count { get; private set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Count++;
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Count++;
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}
