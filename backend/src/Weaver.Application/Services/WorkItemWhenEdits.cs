using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;

namespace Weaver.Application.Services;

/// <summary>Applies <see cref="WhenEditsSinceResolved"/> to a batch of items.</summary>
public class WorkItemWhenEdits(IWeaverDbContext db) : IWorkItemWhenEdits
{
    private readonly IWeaverDbContext _db = db;

    public async Task<IReadOnlySet<Guid>> EditedSinceResolvedAsync(
        IReadOnlyCollection<Guid> workItemIds,
        CancellationToken ct = default)
    {
        if (workItemIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var edited = await _db.WhenEditedSinceResolvedIds().Where(id => workItemIds.Contains(id)).ToListAsync(ct);
        return edited.ToHashSet();
    }
}
