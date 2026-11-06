using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>One Space's configuration, each kind in display order.</summary>
internal sealed record SpaceLookups(
    IReadOnlyList<Status> Statuses,
    IReadOnlyList<WorkItemType> Types,
    IReadOnlyList<WorkItemPriority> Priorities)
{
    public static async Task<SpaceLookups> LoadAsync(IWeaverDbContext db, Guid spaceId, CancellationToken ct) => new(
        await db.Statuses.AsNoTracking().Where(s => s.SpaceId == spaceId).OrderBy(s => s.Order).ToListAsync(ct),
        await db.WorkItemTypes.AsNoTracking().Where(t => t.SpaceId == spaceId).OrderBy(t => t.Order).ToListAsync(ct),
        await db.WorkItemPriorities.AsNoTracking().Where(p => p.SpaceId == spaceId).OrderBy(p => p.Order).ToListAsync(ct));
}
