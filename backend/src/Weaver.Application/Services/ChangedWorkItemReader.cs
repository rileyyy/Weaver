using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Turns a set of history entries into <see cref="ChangedWorkItem"/>s: each item's summary, live,
/// trashed or purged, with its entries. Shared by "changes since T" and run reports, so both
/// describe a changed item the same way.
/// </summary>
internal static class ChangedWorkItemReader
{
    /// <summary>
    /// One <see cref="ChangedWorkItem"/> per id in <paramref name="itemIds"/>, in that order, with
    /// its entries among <paramref name="entries"/>, oldest first.
    /// </summary>
    public static async Task<List<ChangedWorkItem>> ReadAsync(
        IWeaverDbContext db,
        IQueryable<WorkItemHistoryEntry> entries,
        IReadOnlyList<Guid> itemIds,
        CancellationToken ct)
    {
        var withRows = await RowSummariesAsync(db, itemIds, ct);
        var purged = await PurgedSummariesAsync(db, itemIds.Where(id => !withRows.ContainsKey(id)).ToList(), ct);
        var changes = (await HistoryViewReader.ReadAsync(
                db, entries.Where(e => itemIds.Contains(e.WorkItemId)).OrderBy(e => e.Id), ct))
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

    /// <summary>Trashed rows included: a trashed item's summary is still its row, number and all.</summary>
    private static async Task<Dictionary<Guid, WorkItemSummary>> RowSummariesAsync(
        IWeaverDbContext db,
        IReadOnlyList<Guid> itemIds,
        CancellationToken ct) =>
        await db.WorkItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => itemIds.Contains(w.Id))
            .Summarize()
            .ToDictionaryAsync(s => s.Id, ct);

    /// <summary>
    /// A purged item has no row left, so its summary is rebuilt from its history: the title it
    /// had when deleted and the Space it was last in.
    /// </summary>
    private static async Task<Dictionary<Guid, WorkItemSummary>> PurgedSummariesAsync(
        IWeaverDbContext db,
        List<Guid> itemIds,
        CancellationToken ct)
    {
        if (itemIds.Count == 0)
        {
            return [];
        }

        var traces = await DeletedItemTrace.LoadAsync(db, itemIds, ct);
        var spaceIds = traces.Values.Select(t => t.SpaceId).OfType<Guid>().Distinct().ToList();
        var spaceNames = await db.Spaces
            .Where(s => spaceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return itemIds
            .Select(id => traces.GetValueOrDefault(id) ?? DeletedItemTrace.Unknown(id))
            .ToDictionary(
                t => t.WorkItemId,
                t => t.ToSummary(t.SpaceId is { } spaceId ? spaceNames.GetValueOrDefault(spaceId) : null));
    }
}
