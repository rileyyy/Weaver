using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Weaver.Application.Tests;

/// <summary>Lets a test set data up normally, then fails every save once <see cref="Fail"/> is set.</summary>
public sealed class SwitchableSaveFailure : SaveChangesInterceptor
{
    public bool Fail { get; set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        Fail ? throw new InvalidOperationException("The database is down.") : ValueTask.FromResult(result);
}
