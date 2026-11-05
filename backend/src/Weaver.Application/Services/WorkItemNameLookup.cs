using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// One query per kind of id the items hold (at most six, whatever the batch size), each skipped
/// when no item holds that kind.
/// </summary>
public class WorkItemNameLookup(IWeaverDbContext db) : IWorkItemNameLookup
{
    private readonly IWeaverDbContext _db = db;

    public async Task<WorkItemNames> NamesForAsync(IReadOnlyCollection<WorkItem> items, CancellationToken ct = default)
    {
        var spaceIds = IdsOf(items, i => i.SpaceId);
        var statusIds = IdsOf(items, i => i.StatusId);
        var typeIds = IdsOf(items, i => i.TypeId);
        var priorityIds = IdsOf(items, i => i.PriorityId);
        var userIds = IdsOf(items, i => i.AssignedToUserId);
        var parentIds = IdsOf(items, i => i.ParentId);

        return new WorkItemNames
        {
            Spaces = await NamesAsync(_db.Spaces.Where(s => spaceIds.Contains(s.Id)).Select(s => new IdAndName(s.Id, s.Name)), spaceIds, ct),
            Statuses = await LookupNamesAsync(_db.Statuses, statusIds, ct),
            Types = await LookupNamesAsync(_db.WorkItemTypes, typeIds, ct),
            Priorities = await LookupNamesAsync(_db.WorkItemPriorities, priorityIds, ct),
            Usernames = await NamesAsync(_db.Users.Where(u => userIds.Contains(u.Id)).Select(u => new IdAndName(u.Id, u.Username)), userIds, ct),
            WorkItems = await WorkItemReferencesAsync(parentIds, ct),
        };
    }

    private static List<Guid> IdsOf(IEnumerable<WorkItem> items, Func<WorkItem, Guid?> id) =>
        items.Select(id).OfType<Guid>().Distinct().ToList();

    private static Task<IReadOnlyDictionary<Guid, string>> LookupNamesAsync<T>(
        IQueryable<T> values,
        List<Guid> ids,
        CancellationToken ct)
        where T : LookupValue =>
        NamesAsync(values.Where(v => ids.Contains(v.Id)).Select(v => new IdAndName(v.Id, v.Name)), ids, ct);

    private static async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(
        IQueryable<IdAndName> names,
        List<Guid> ids,
        CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return (await names.ToListAsync(ct)).ToDictionary(n => n.Id, n => n.Name);
    }

    // A trashed parent is still named: a reader of trashed items sees their trashed parents.
    private async Task<IReadOnlyDictionary<Guid, WorkItemReference>> WorkItemReferencesAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, WorkItemReference>();
        }

        var references = await _db.WorkItems
            .IgnoreQueryFilters()
            .Where(w => ids.Contains(w.Id))
            .Select(w => new { w.Id, w.Number, w.Title })
            .ToListAsync(ct);
        return references.ToDictionary(r => r.Id, r => new WorkItemReference(r.Number, r.Title));
    }

    private sealed record IdAndName(Guid Id, string Name);
}
