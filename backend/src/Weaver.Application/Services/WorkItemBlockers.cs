using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;

namespace Weaver.Application.Services;

/// <summary>A blocker in the Trash stops counting until it's restored; see <see cref="BlockedWorkItems"/>.</summary>
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
            .BlockedIds()
            .Where(id => workItemIds.Contains(id))
            .Distinct()
            .ToListAsync(ct);
        return blocked.ToHashSet();
    }
}
