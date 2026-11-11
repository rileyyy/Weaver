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

        var items = await ChangedWorkItemReader.ReadAsync(_db, entries, onPage.Select(p => p.WorkItemId).ToList(), ct);
        return new WorkItemChangesPage(items, nextCursor);
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
}
