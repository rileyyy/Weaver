using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;

namespace Weaver.Application.Services;

/// <summary>One query over the live blocks links; a blocker in the Trash isn't one.</summary>
public class DependencyConflicts(IWeaverDbContext db) : IDependencyConflicts
{
    private readonly IWeaverDbContext _db = db;

    public async Task<IReadOnlyList<DependencyConflict>> ListAsync(Guid? spaceId = null, CancellationToken ct = default)
    {
        if (spaceId is not null)
        {
            await _db.EnsureSpaceExistsAsync(spaceId.Value, ct);
        }

        var rows = await InSpace(_db.WorkItemLinks.LiveBlocksLinkRows().Where(r => r.Conflicts), spaceId)
            .OrderBy(r => r.BlockedStartDate)
            .ThenBy(r => r.BlockedNumber)
            .ThenBy(r => r.BlockerNumber)
            .ToListAsync(ct);
        return rows.Select(r => r.ToConflict()).ToList();
    }

    private static IQueryable<BlocksLinkRow> InSpace(IQueryable<BlocksLinkRow> rows, Guid? spaceId) =>
        spaceId is null ? rows : rows.Where(r => r.BlockedSpaceId == spaceId || r.BlockerSpaceId == spaceId);
}
