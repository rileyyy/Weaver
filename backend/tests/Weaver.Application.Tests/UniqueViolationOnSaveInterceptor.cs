using Microsoft.EntityFrameworkCore.Diagnostics;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Tests;

/// <summary>
/// Fails every save the way the real context reports a Postgres unique violation, which the
/// InMemory provider can't produce, to stand in for another writer winning a race.
/// </summary>
public class UniqueViolationOnSaveInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        throw new UniqueConstraintViolationException("IX_Users_NormalizedUsername", new Exception("unique violation"));
}
