using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Weaver.Domain;

namespace Weaver.Infrastructure;

/// <summary>
/// Sets <see cref="IHasCreatedAt.CreatedAtUtc"/> on insert and <see cref="IHasUpdatedAt.UpdatedAtUtc"/>
/// on insert and update, from one clock, just before each save.
/// </summary>
public class TimestampInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _clock;

    public TimestampInterceptor(TimeProvider clock)
    {
        _clock = clock;
    }

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

        var now = _clock.GetUtcNow();
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
}
