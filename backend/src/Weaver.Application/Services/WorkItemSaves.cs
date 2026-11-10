using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

internal static class WorkItemSaves
{
    /// <summary>
    /// Saves a write to work items, reporting a row someone else changed since it was read
    /// (its <c>xmin</c> moved on) as a conflict on <paramref name="workItemId"/>, the item the
    /// caller asked to change.
    /// </summary>
    public static async Task SaveWorkItemChangesAsync(this IWeaverDbContext db, Guid workItemId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new WorkItemVersionConflictException(workItemId, ex);
        }
    }
}
