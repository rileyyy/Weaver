using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// <see cref="IWorkItemHierarchy"/> for the EF InMemory provider, which can't run the
/// recursive SQL the real implementation uses. Walks one level per query with the same
/// contract; <c>Weaver.Api.IntegrationTests</c> covers the Postgres implementation.
/// </summary>
public class IterativeWorkItemHierarchy : IWorkItemHierarchy
{
    private readonly WeaverDbContext _db;

    public IterativeWorkItemHierarchy(WeaverDbContext db)
    {
        _db = db;
    }

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
                .Where(w => w.Id == currentId)
                .Select(w => w.ParentId)
                .FirstOrDefaultAsync(ct);
        }

        return false;
    }

    public async Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid rootId, CancellationToken ct = default)
    {
        var descendants = new HashSet<Guid>();
        var frontier = new List<Guid> { rootId };

        while (frontier.Count > 0)
        {
            var children = await _db.WorkItems
                .Where(w => w.ParentId != null && frontier.Contains(w.ParentId.Value))
                .Select(w => w.Id)
                .ToListAsync(ct);

            frontier = children.Where(descendants.Add).ToList();
        }

        return descendants.ToList();
    }
}
