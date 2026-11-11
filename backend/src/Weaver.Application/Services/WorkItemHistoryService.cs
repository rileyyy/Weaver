using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemHistoryService(IWeaverDbContext db) : IWorkItemHistoryService
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    private readonly IWeaverDbContext _db = db;

    public async Task<IReadOnlyList<WorkItemHistoryView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var entries = await HistoryViewReader.ReadAsync(_db, NewestFirst(workItemId), ct);
        await EnsureFoundAsync(workItemId, entries, ct);
        return entries;
    }

    public async Task<WorkItemHistoryPage> ListPageForWorkItemAsync(
        Guid workItemId,
        int offset,
        int limit,
        CancellationToken ct = default)
    {
        PagingGuard.EnsureValid(offset, limit, MaxPageSize);

        // One extra entry says whether another page follows, without counting them all.
        var entries = await HistoryViewReader.ReadAsync(_db, NewestFirst(workItemId).Skip(offset).Take(limit + 1), ct);
        await EnsureFoundAsync(workItemId, entries, ct);

        var hasMore = entries.Count > limit;
        return new WorkItemHistoryPage(entries.Take(limit).ToList(), hasMore ? offset + limit : null);
    }

    private IQueryable<WorkItemHistoryEntry> NewestFirst(Guid workItemId) =>
        _db.WorkItemHistory.Where(e => e.WorkItemId == workItemId).OrderByDescending(e => e.Id);

    /// <summary>History outlives the item, so a trashed one's is still readable.</summary>
    private async Task EnsureFoundAsync(Guid workItemId, IReadOnlyCollection<WorkItemHistoryView> entries, CancellationToken ct)
    {
        if (entries.Count == 0 && !await _db.WorkItems.IgnoreQueryFilters().AnyAsync(w => w.Id == workItemId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), workItemId);
        }
    }
}
