using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemChangesService(IWeaverDbContext db, ICallerContext caller) : IWorkItemChangesService
{
    private readonly IWeaverDbContext _db = db;
    private readonly ICallerContext _caller = caller;

    public async Task<WorkItemChangesPage> ListSinceAsync(WorkItemChangesQuery query, CancellationToken ct = default)
    {
        if (query.Limit is < 1 or > WorkItemChangesQuery.MaxLimit)
        {
            throw new DomainValidationException($"Limit must be between 1 and {WorkItemChangesQuery.MaxLimit}.");
        }

        var afterEntryId = WorkItemChangesCursor.Parse(query.Cursor).AfterEntryId;
        var entries = await EntriesSinceAsync(query, ct);

        // By first entry since T, not by time: see WorkItemChangesCursor.
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
        var withRows = await RowSummariesAsync(itemIds, ct);
        var purged = await PurgedSummariesAsync(itemIds.Where(id => !withRows.ContainsKey(id)).ToList(), ct);
        var changes = (await HistoryViewReader.ReadAsync(
                _db, entries.Where(e => itemIds.Contains(e.WorkItemId)).OrderBy(e => e.Id), ct))
            .ToLookup(c => c.WorkItemId);

        return itemIds
            .Select(id =>
            {
                var itemChanges = changes[id].ToList();
                if (!withRows.TryGetValue(id, out var summary))
                {
                    return new ChangedWorkItem(purged[id], Deleted: true, Restored: false, itemChanges);
                }

                var trashed = summary.TrashedAtUtc is not null;
                var restored = !trashed && itemChanges.Any(c => c.Field == WorkItemHistoryField.Restored);
                return new ChangedWorkItem(summary, Deleted: trashed, Restored: restored, itemChanges);
            })
            .ToList();
    }

    private async Task<IQueryable<WorkItemHistoryEntry>> EntriesSinceAsync(WorkItemChangesQuery query, CancellationToken ct)
    {
        // Npgsql only accepts a UTC DateTimeOffset, and agents send times in their own offset.
        var since = query.Since.ToUniversalTime();
        var entries = _db.WorkItemHistory.Where(e => e.CreatedAtUtc >= since);
        if (query.ExcludeOwnChanges)
        {
            // Weaver's own entries have no account and must stay: the null check says so rather
            // than leaving it to EF's translation of "<>" on a nullable column.
            var me = _caller.Current.AccountId;
            entries = entries.Where(e => e.AccountId == null || e.AccountId != me);
        }

        return query.SpaceId is { } spaceId ? await InSpaceAsync(entries, spaceId, ct) : entries;
    }

    /// <summary>
    /// An item with a row, live or trashed, is in the Space its row says. A purged one has no row,
    /// so it's in the Space its history last put it in, worked out here for just the purged items
    /// changed since T.
    /// </summary>
    private async Task<IQueryable<WorkItemHistoryEntry>> InSpaceAsync(
        IQueryable<WorkItemHistoryEntry> entries,
        Guid spaceId,
        CancellationToken ct)
    {
        var rows = _db.WorkItems.IgnoreQueryFilters();
        var purgedIds = await entries
            .Where(e => !rows.Any(w => w.Id == e.WorkItemId))
            .Select(e => e.WorkItemId)
            .Distinct()
            .ToListAsync(ct);
        var purgedInSpace = (await DeletedItemTrace.LoadAsync(_db, purgedIds, ct)).Values
            .Where(t => t.SpaceId == spaceId)
            .Select(t => t.WorkItemId)
            .ToList();

        return entries.Where(e =>
            rows.Any(w => w.Id == e.WorkItemId && w.SpaceId == spaceId) || purgedInSpace.Contains(e.WorkItemId));
    }

    /// <summary>Trashed rows included: a trashed item's summary is still its row, number and all.</summary>
    private async Task<Dictionary<Guid, WorkItemSummary>> RowSummariesAsync(List<Guid> itemIds, CancellationToken ct) =>
        await _db.WorkItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => itemIds.Contains(w.Id))
            .Summarize()
            .ToDictionaryAsync(s => s.Id, ct);

    /// <summary>
    /// A purged item has no row left, so its summary is rebuilt from its history: the title it
    /// had when deleted and the Space it was last in.
    /// </summary>
    private async Task<Dictionary<Guid, WorkItemSummary>> PurgedSummariesAsync(List<Guid> itemIds, CancellationToken ct)
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
            .Select(id => traces.GetValueOrDefault(id) ?? DeletedItemTrace.Unknown(id))
            .ToDictionary(
                t => t.WorkItemId,
                t => t.ToSummary(t.SpaceId is { } spaceId ? spaceNames.GetValueOrDefault(spaceId) : null));
    }
}
