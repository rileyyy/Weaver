using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemTrashService(
    IWeaverDbContext db,
    IWorkItemHierarchy hierarchy,
    ICallerContext caller,
    TimeProvider clock) : IWorkItemTrashService
{
    private readonly IWeaverDbContext _db = db;
    private readonly IWorkItemHierarchy _hierarchy = hierarchy;
    private readonly TimeProvider _clock = clock;
    private readonly WorkItemHistoryRecorder _history = new WorkItemHistoryRecorder(db, caller);

    public async Task MoveToTrashAsync(Guid id, bool cascade = false, uint? expectedVersion = null, CancellationToken ct = default)
    {
        // Not FindAsync: it returns an entity already tracked even when the Trash filter would
        // hide it, so an item trashed earlier in this context would look live.
        var item = await _db.WorkItems.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);
        if (expectedVersion is not null && item.Version != expectedVersion)
        {
            throw new WorkItemVersionConflictException(item.Id);
        }

        var descendantIds = await _hierarchy.GetDescendantIdsAsync(id, ct);
        var descendants = await _db.WorkItems.Where(w => descendantIds.Contains(w.Id)).ToListAsync(ct);
        if (descendants.Count > 0 && !cascade)
        {
            throw new WorkItemHasChildrenException(id);
        }

        var now = _clock.GetUtcNow();
        foreach (var trashed in descendants.Prepend(item))
        {
            trashed.MoveToTrash(id, now);
            _history.Record(trashed.Id, WorkItemHistoryField.Deleted, trashed.Title, null);
        }

        await _db.SaveWorkItemChangesAsync(id, ct);
    }

    public async Task<IReadOnlyList<TrashedWorkItem>> ListAsync(Guid? spaceId = null, CancellationToken ct = default)
    {
        var roots = Trash().Where(w => w.TrashRootId == w.Id);
        if (spaceId is { } space)
        {
            roots = roots.Where(w => w.SpaceId == space);
        }

        var summaries = await roots.AsNoTracking().OrderByDescending(w => w.TrashedAtUtc).Summarize().ToListAsync(ct);
        var rootIds = summaries.Select(s => s.Id).ToList();
        var itemCounts = await Trash()
            .Where(w => w.TrashRootId != null && rootIds.Contains(w.TrashRootId.Value))
            .GroupBy(w => w.TrashRootId!.Value)
            .Select(g => new { RootId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RootId, g => g.Count, ct);
        var deletions = await LatestDeletionsAsync(rootIds, ct);

        return summaries
            .Select(s =>
            {
                var deletion = deletions.GetValueOrDefault(s.Id);
                return new TrashedWorkItem(
                    s,
                    s.TrashedAtUtc!.Value,
                    itemCounts.GetValueOrDefault(s.Id, 1),
                    deletion?.AccountName,
                    deletion?.AccountKind,
                    deletion?.ActorLabel);
            })
            .ToList();
    }

    /// <summary>
    /// Who deleted each item comes from its history (ADR-0005) rather than a column: an account
    /// with history can't be hard-deleted, so the name is always there to show.
    /// </summary>
    private async Task<Dictionary<Guid, WorkItemHistoryView>> LatestDeletionsAsync(List<Guid> itemIds, CancellationToken ct)
    {
        var entries = await HistoryViewReader.ReadAsync(
            _db,
            _db.WorkItemHistory
                .Where(e => itemIds.Contains(e.WorkItemId) && e.Field == WorkItemHistoryField.Deleted)
                .OrderBy(e => e.Id),
            ct);
        return entries.GroupBy(e => e.WorkItemId).ToDictionary(g => g.Key, g => g.Last());
    }

    public async Task<WorkItem> RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.WorkItems.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);
        if (!item.IsTrashed)
        {
            throw new DomainValidationException($"Work item {id} isn't in the Trash.");
        }

        if (!item.IsTrashRoot)
        {
            throw new DomainValidationException(
                $"Work item {id} was deleted along with work item {item.TrashRootId}; restore that one instead.");
        }

        var trashedWith = await Trash().Where(w => w.TrashRootId == id).ToListAsync(ct);
        foreach (var restored in trashedWith)
        {
            restored.RestoreFromTrash();
            _history.Record(restored.Id, WorkItemHistoryField.Restored, null, restored.Title);
        }

        await PlaceUnderNearestLiveAncestorAsync(item, ct);
        await RestartRecurrencesAsync(trashedWith.Select(w => w.Id).ToList(), ct);
        await _db.SaveWorkItemChangesAsync(id, ct);
        return item;
    }

    /// <summary>
    /// A live item never sits under a trashed one, so if the parent is still in the Trash (or
    /// purged) the item moves up to the first ancestor that's live, at the end of its new cell.
    /// </summary>
    private async Task PlaceUnderNearestLiveAncestorAsync(WorkItem item, CancellationToken ct)
    {
        var parent = await NearestLiveAncestorAsync(item.ParentId, ct);
        if (parent?.Id == item.ParentId)
        {
            return;
        }

        var newParentId = parent?.Id;
        var lastRank = await _db.WorkItems
            .Where(w => w.SpaceId == item.SpaceId && w.ParentId == newParentId && w.StatusId == item.StatusId)
            .Select(w => (double?)w.Rank)
            .MaxAsync(ct);
        _history.RecordChanges(item.Id, item.MoveToParent(parent, RankCalculator.GetRankBetween(lastRank, null)));
    }

    private async Task<WorkItem?> NearestLiveAncestorAsync(Guid? parentId, CancellationToken ct)
    {
        var visited = new HashSet<Guid>();
        while (parentId is { } id && visited.Add(id))
        {
            var ancestor = await _db.WorkItems.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == id, ct);
            if (ancestor is null || !ancestor.IsTrashed)
            {
                return ancestor;
            }

            parentId = ancestor.ParentId;
        }

        return null;
    }

    private async Task RestartRecurrencesAsync(List<Guid> restoredIds, CancellationToken ct)
    {
        var recurrences = await _db.WorkItemRecurrences
            .IgnoreQueryFilters()
            .Where(r => restoredIds.Contains(r.WorkItemId))
            .ToListAsync(ct);
        foreach (var recurrence in recurrences)
        {
            recurrence.RestartFromToday();
        }
    }

    private IQueryable<WorkItem> Trash() => _db.WorkItems.IgnoreQueryFilters().Where(w => w.TrashedAtUtc != null);
}
