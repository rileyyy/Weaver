using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// <see cref="IWorkItemHierarchy"/> for the EF InMemory provider, which can't run the
/// recursive SQL the real implementation uses. Walks one level per query with the same
/// contract, including which walks see trashed rows (raw SQL skips EF's Trash filter);
/// <c>Weaver.Api.IntegrationTests</c> covers the Postgres implementation.
/// </summary>
public class IterativeWorkItemHierarchy(WeaverDbContext db) : IWorkItemHierarchy
{
    private readonly WeaverDbContext _db = db;

    public async Task<bool> IsSelfOrDescendantAsync(Guid itemId, Guid candidateId, CancellationToken ct = default)
    {
        Guid? currentId = candidateId;
        var visited = new HashSet<Guid>();

        while (currentId is not null)
        {
            if (currentId == itemId)
            {
                return true;
            }

            if (!visited.Add(currentId.Value))
            {
                throw new InvalidOperationException(
                    $"The work item hierarchy contains a cycle through work item {currentId}.");
            }

            currentId = await _db.WorkItems
                .IgnoreQueryFilters()
                .Where(w => w.Id == currentId)
                .Select(w => w.ParentId)
                .FirstOrDefaultAsync(ct);
        }

        return false;
    }

    public Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid rootId, CancellationToken ct = default) =>
        DescendantIdsAsync(rootId, includeTrashed: false, ct);

    public Task<IReadOnlyList<Guid>> GetDescendantIdsIncludingTrashAsync(Guid rootId, CancellationToken ct = default) =>
        DescendantIdsAsync(rootId, includeTrashed: true, ct);

    private async Task<IReadOnlyList<Guid>> DescendantIdsAsync(Guid rootId, bool includeTrashed, CancellationToken ct)
    {
        var descendants = new HashSet<Guid>();
        var frontier = new List<Guid> { rootId };
        var items = includeTrashed ? _db.WorkItems.IgnoreQueryFilters() : _db.WorkItems;

        while (frontier.Count > 0)
        {
            var children = await items
                .Where(w => w.ParentId != null && frontier.Contains(w.ParentId.Value))
                .Select(w => w.Id)
                .ToListAsync(ct);

            frontier = children.Where(descendants.Add).ToList();
        }

        return descendants.ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetSelfAndAncestorIdsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct = default)
    {
        var found = new HashSet<Guid>();
        var frontier = itemIds.ToList();

        while (frontier.Count > 0)
        {
            var rows = await _db.WorkItems
                .IgnoreQueryFilters()
                .Where(w => frontier.Contains(w.Id))
                .Select(w => new { w.Id, w.ParentId })
                .ToListAsync(ct);

            frontier = rows
                .Where(r => found.Add(r.Id) && r.ParentId is not null)
                .Select(r => r.ParentId!.Value)
                .Where(id => !found.Contains(id))
                .ToList();
        }

        return found.ToList();
    }
}
