using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;

namespace Weaver.Application.Services;

/// <summary>A blocker in the Trash stops counting until it's restored; see <see cref="BlockedWorkItems"/>.</summary>
public class WorkItemBlockers(IWeaverDbContext db) : IWorkItemBlockers
{
    private readonly IWeaverDbContext _db = db;

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<WorkItemBlocker>>> BlockersOfAsync(
        IReadOnlyCollection<Guid> workItemIds,
        CancellationToken ct = default)
    {
        if (workItemIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<WorkItemBlocker>>();
        }

        var rows = await _db.WorkItemLinks
            .LiveBlocksLinkRows()
            .Where(r => workItemIds.Contains(r.BlockedId))
            .OrderBy(r => r.BlockerNumber)
            .ToListAsync(ct);
        return rows
            .GroupBy(r => r.BlockedId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<WorkItemBlocker>)g.Select(r => r.ToBlocker()).ToList());
    }
}
