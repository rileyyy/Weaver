using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

internal static class WorkItemExistence
{
    /// <summary>A trashed item counts as missing, since the Trash query filter hides it.</summary>
    public static async Task EnsureWorkItemExistsAsync(this IWeaverDbContext db, Guid workItemId, CancellationToken ct)
    {
        if (!await db.WorkItems.AnyAsync(w => w.Id == workItemId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), workItemId);
        }
    }
}
