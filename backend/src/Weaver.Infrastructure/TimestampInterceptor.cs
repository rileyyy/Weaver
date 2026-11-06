using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Weaver.Domain;

namespace Weaver.Infrastructure;

/// <summary>
/// Sets <see cref="IHasCreatedAt.CreatedAtUtc"/> on insert and <see cref="IHasUpdatedAt.UpdatedAtUtc"/>
/// on insert and update, from one clock, just before each save.
/// </summary>
/// <remarks>
/// The time is cut to whole microseconds, the precision Postgres keeps, so the value an entity
/// holds after saving (and returns to the client that made it) is the one every later read sees.
/// </remarks>
public class TimestampInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    private readonly TimeProvider _clock = clock;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = ToMicroseconds(_clock.GetUtcNow());
        foreach (var entry in context.ChangeTracker.Entries<IHasCreatedAt>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IHasCreatedAt.CreatedAtUtc)).CurrentValue = now;
            }

            if (entry.Entity is IHasUpdatedAt && entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(nameof(IHasUpdatedAt.UpdatedAtUtc)).CurrentValue = now;
            }
        }
    }

    private static DateTimeOffset ToMicroseconds(DateTimeOffset time) =>
        time.AddTicks(-(time.Ticks % TimeSpan.TicksPerMicrosecond));
}
