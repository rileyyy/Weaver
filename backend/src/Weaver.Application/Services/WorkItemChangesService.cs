using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemChangesService : IWorkItemChangesService
{
    private readonly IWeaverDbContext _db;
    private readonly ICallerContext _caller;

    public WorkItemChangesService(IWeaverDbContext db, ICallerContext caller)
    {
        _db = db;
        _caller = caller;
    }

    public async Task<WorkItemChangesPage> ListSinceAsync(WorkItemChangesQuery query, CancellationToken ct = default)
    {
        if (query.Limit is < 1 or > WorkItemChangesQuery.MaxLimit)
        {
            throw new DomainValidationException($"Limit must be between 1 and {WorkItemChangesQuery.MaxLimit}.");
        }

        var afterEntryId = WorkItemChangesCursor.Parse(query.Cursor).AfterEntryId;
        var entries = await EntriesSinceAsync(query, ct);

        // Items are ordered, and the cursor placed, by each item's first entry since T: entry ids
        // only grow and that first entry never changes, so later changes can't reorder pages.
        var positions = await entries
            .GroupBy(e => e.WorkItemId)
            .Select(g => new { WorkItemId = g.Key, FirstEntryId = g.Min(e => e.Id) })
            .Where(i => i.FirstEntryId > afterEntryId)
            .OrderBy(i => i.FirstEntryId)
            .Take(query.Limit + 1)
            .ToListAsync(ct);
        var onPage = positions.Take(query.Limit).ToList();
        var nextCursor = positions.Count > query.Limit
            ? new WorkItemChangesCursor(onPage[^1].FirstEntryId).Encode()
            : null;

        var items = await ChangedItemsAsync(entries, onPage.Select(p => p.WorkItemId).ToList(), ct);
        return new WorkItemChangesPage(items, nextCursor);
    }

    private async Task<List<ChangedWorkItem>> ChangedItemsAsync(
        IQueryable<WorkItemHistoryEntry> entries,
        List<Guid> itemIds,
        CancellationToken ct)
    {
        var live = await LiveSummariesAsync(itemIds, ct);
        var deleted = await DeletedSummariesAsync(itemIds.Where(id => !live.ContainsKey(id)).ToList(), ct);
        var changes = (await HistoryViewReader.ReadAsync(
                _db, entries.Where(e => itemIds.Contains(e.WorkItemId)).OrderBy(e => e.Id), ct))
            .ToLookup(c => c.WorkItemId);

        return itemIds
            .Select(id => live.TryGetValue(id, out var summary)
                ? new ChangedWorkItem(summary, Deleted: false, changes[id].ToList())
                : new ChangedWorkItem(deleted[id], Deleted: true, changes[id].ToList()))
            .ToList();
    }

    private async Task<IQueryable<WorkItemHistoryEntry>> EntriesSinceAsync(WorkItemChangesQuery query, CancellationToken ct)
    {
        var entries = _db.WorkItemHistory.Where(e => e.CreatedAtUtc >= query.Since);
        if (query.ExcludeOwnChanges)
        {
            // Weaver's own entries have no account and must stay. Spelled out, since in SQL a bare
            // "AccountId <> me" is false for a null account.
            var me = _caller.Current.AccountId;
            entries = entries.Where(e => e.AccountId == null || e.AccountId != me);
        }

        return query.SpaceId is { } spaceId ? await InSpaceAsync(entries, spaceId, ct) : entries;
    }

    /// <summary>
    /// A live item is in the Space its row says. A deleted one has no row, so it's in the Space
    /// its history last put it in, worked out here for just the deleted items changed since T.
    /// </summary>
    private async Task<IQueryable<WorkItemHistoryEntry>> InSpaceAsync(
        IQueryable<WorkItemHistoryEntry> entries,
        Guid spaceId,
        CancellationToken ct)
    {
        var deletedIds = await entries
            .Where(e => !_db.WorkItems.Any(w => w.Id == e.WorkItemId))
            .Select(e => e.WorkItemId)
            .Distinct()
            .ToListAsync(ct);
        var deletedInSpace = (await DeletedItemTrace.LoadAsync(_db, deletedIds, ct)).Values
            .Where(t => t.SpaceId == spaceId)
            .Select(t => t.WorkItemId)
            .ToList();

        return entries.Where(e =>
            _db.WorkItems.Any(w => w.Id == e.WorkItemId && w.SpaceId == spaceId) || deletedInSpace.Contains(e.WorkItemId));
    }

    private async Task<Dictionary<Guid, WorkItemSummary>> LiveSummariesAsync(List<Guid> itemIds, CancellationToken ct) =>
        await _db.WorkItems
            .AsNoTracking()
            .Where(w => itemIds.Contains(w.Id))
            .Select(w => new WorkItemSummary(
                w.Id,
                w.Number,
                w.Title,
                w.SpaceId,
                w.Space!.Name,
                w.Status!.Name,
                w.Layer == null ? null : w.Layer.Name,
                w.StartDate,
                w.EndDate,
                w.AssignedToUser == null ? null : w.AssignedToUser.Username,
                w.Children.Count()))
            .ToDictionaryAsync(s => s.Id, ct);

    /// <summary>
    /// A deleted item has no row left, so its summary is rebuilt from its history: the title it
    /// had when deleted and the Space it was last in.
    /// </summary>
    private async Task<Dictionary<Guid, WorkItemSummary>> DeletedSummariesAsync(List<Guid> itemIds, CancellationToken ct)
    {
        if (itemIds.Count == 0)
        {
            return [];
        }

        var traces = await DeletedItemTrace.LoadAsync(_db, itemIds, ct);
        var spaceIds = traces.Values.Select(t => t.SpaceId).OfType<Guid>().Distinct().ToList();
        var spaceNames = await _db.Spaces
            .Where(s => spaceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return itemIds
            .Select(id => traces.GetValueOrDefault(id) ?? new DeletedItemTrace(id, string.Empty, null))
            .ToDictionary(
                t => t.WorkItemId,
                t => new WorkItemSummary(
                    t.WorkItemId,
                    Number: null,
                    t.Title,
                    t.SpaceId,
                    t.SpaceId is { } spaceId ? spaceNames.GetValueOrDefault(spaceId) : null,
                    StatusName: null,
                    LayerName: null,
                    StartDate: null,
                    EndDate: null,
                    AssigneeName: null,
                    ChildCount: null));
    }
}
