using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;

namespace Weaver.Infrastructure;

/// <summary>
/// One recursive query per walk, instead of one round trip per tree level.
/// </summary>
public class PostgresWorkItemHierarchy(WeaverDbContext db) : IWorkItemHierarchy
{
    private readonly WeaverDbContext _db = db;

    public async Task<bool> IsSelfOrDescendantAsync(Guid itemId, Guid candidateId, CancellationToken ct = default)
    {
        // Walks up from the candidate. CYCLE stops the walk if it revisits a row and flags that
        // row, so a corrupt tree ends the query instead of recursing forever.
        var ancestors = await _db.Database
            .SqlQuery<AncestorRow>($"""
                WITH RECURSIVE ancestors ("Id", "ParentId") AS (
                    SELECT "Id", "ParentId" FROM "WorkItems" WHERE "Id" = {candidateId}
                    UNION ALL
                    SELECT w."Id", w."ParentId"
                    FROM "WorkItems" w
                    JOIN ancestors a ON w."Id" = a."ParentId"
                ) CYCLE "Id" SET "IsCycle" USING "Path"
                SELECT "Id", "IsCycle" FROM ancestors
                """)
            .ToListAsync(ct);

        if (ancestors.Any(a => a.Id == itemId))
        {
            return true;
        }

        var cycle = ancestors.FirstOrDefault(a => a.IsCycle);
        return cycle is null
            ? false
            : throw new InvalidOperationException(
                $"The work item hierarchy contains a cycle through work item {cycle.Id}.");
    }

    public async Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid rootId, CancellationToken ct = default) =>
        // UNION (not UNION ALL) drops rows already seen, so this terminates even on a corrupt tree.
        // Raw SQL skips EF's Trash filter, so it's repeated here: a subtree trashed on its own
        // earlier isn't part of this one.
        await _db.Database
            .SqlQuery<Guid>($"""
                WITH RECURSIVE descendants ("Id") AS (
                    SELECT "Id" FROM "WorkItems" WHERE "ParentId" = {rootId} AND "TrashedAtUtc" IS NULL
                    UNION
                    SELECT w."Id"
                    FROM "WorkItems" w
                    JOIN descendants d ON w."ParentId" = d."Id"
                    WHERE w."TrashedAtUtc" IS NULL
                )
                SELECT "Id" AS "Value" FROM descendants
                """)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetSelfAndAncestorIdsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct = default)
    {
        if (itemIds.Count == 0)
        {
            return [];
        }

        var ids = itemIds.ToArray();
        // UNION (not UNION ALL) drops rows already seen, so this terminates even on a corrupt tree.
        return await _db.Database
            .SqlQuery<Guid>($"""
                WITH RECURSIVE ancestors ("Id", "ParentId") AS (
                    SELECT "Id", "ParentId" FROM "WorkItems" WHERE "Id" = ANY({ids})
                    UNION
                    SELECT w."Id", w."ParentId"
                    FROM "WorkItems" w
                    JOIN ancestors a ON w."Id" = a."ParentId"
                )
                SELECT DISTINCT "Id" AS "Value" FROM ancestors
                """)
            .ToListAsync(ct);
    }
}
