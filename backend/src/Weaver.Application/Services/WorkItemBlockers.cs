using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// The links' Trash filter hides a link while either end is trashed, so a trashed blocker
/// stops counting without anything here having to know about the Trash.
/// </summary>
public class WorkItemBlockers(IWeaverDbContext db) : IWorkItemBlockers
{
    private readonly IWeaverDbContext _db = db;

    public async Task<IReadOnlySet<Guid>> BlockedAmongAsync(IReadOnlyCollection<Guid> workItemIds, CancellationToken ct = default)
    {
        if (workItemIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var blocked = await _db.WorkItemLinks
            .Where(l => l.Type == WorkItemLinkType.Blocks && workItemIds.Contains(l.LinkedWorkItemId))
            .Select(l => l.LinkedWorkItemId)
            .Distinct()
            .ToListAsync(ct);
        return blocked.ToHashSet();
    }
}
