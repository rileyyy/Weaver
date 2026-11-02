using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemService : IWorkItemService
{
    public const int DefaultPageSize = 500;
    public const int MaxPageSize = 1000;

    private readonly IWeaverDbContext _db;
    private readonly IWorkItemHierarchy _hierarchy;
    private readonly WorkItemHistoryRecorder _history;

    public WorkItemService(IWeaverDbContext db, IWorkItemHierarchy hierarchy, ICallerContext caller)
    {
        _db = db;
        _hierarchy = hierarchy;
        _history = new WorkItemHistoryRecorder(db, caller);
    }

    public Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.WorkItems.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<IReadOnlyList<WorkItem>> GetChildrenAsync(Guid? parentId, CancellationToken ct = default) =>
        await _db.WorkItems
            .AsNoTracking()
            .Where(w => w.ParentId == parentId)
            .OrderBy(w => w.Rank)
            .ThenBy(w => w.Number)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Swimlane>> GetSwimlanesAsync(Guid? scopeItemId, CancellationToken ct = default)
    {
        var lanes = await _db.WorkItems
            .AsNoTracking()
            .Where(w => w.ParentId == scopeItemId)
            .OrderBy(w => w.Rank)
            .ThenBy(w => w.Number)
            .ToListAsync(ct);

        var laneIds = lanes.Select(l => l.Id).ToList();
        var cardsByLane = (await _db.WorkItems
                .AsNoTracking()
                .Where(w => w.ParentId != null && laneIds.Contains(w.ParentId.Value))
                .OrderBy(w => w.Rank)
                .ThenBy(w => w.Number)
                .ToListAsync(ct))
            .ToLookup(w => w.ParentId!.Value);

        return lanes.Select(lane => new Swimlane(lane, cardsByLane[lane.Id].ToList())).ToList();
    }

    public async Task<IReadOnlyList<WorkItem>> GetAllAsync(int offset, int limit, CancellationToken ct = default)
    {
        if (offset < 0)
        {
            throw new DomainValidationException("Offset must not be negative.");
        }

        if (limit is < 1 or > MaxPageSize)
        {
            throw new DomainValidationException($"Limit must be between 1 and {MaxPageSize}.");
        }

        return await _db.WorkItems
            .AsNoTracking()
            .OrderBy(w => w.Rank)
            .ThenBy(w => w.Number)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<WorkItem> CreateAsync(
        string title,
        string? description,
        Guid? parentId,
        Guid statusId,
        Guid? afterId = null,
        Guid? layerId = null,
        Guid? priorityId = null,
        CancellationToken ct = default)
    {
        if (parentId is not null && !await _db.WorkItems.AnyAsync(w => w.Id == parentId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), parentId.Value);
        }

        if (!await _db.Statuses.AnyAsync(s => s.Id == statusId, ct))
        {
            throw new EntityNotFoundException(nameof(Status), statusId);
        }

        await EnsureDetailLookupsExistAsync(layerId, priorityId, ct);

        var rank = await ComputeRankAsync(parentId, statusId, afterId, excludeItemId: null, ct);
        var item = WorkItem.Create(title, description, parentId, statusId, rank, layerId, priorityId);

        _db.WorkItems.Add(item);
        _history.Record(item.Id, WorkItemHistoryField.Created, null, item.Title);
        await _db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<WorkItemWriteResult> ChangeStatusAsync(
        Guid id,
        Guid newStatusId,
        Guid? afterId = null,
        uint? expectedVersion = null,
        CancellationToken ct = default)
    {
        var item = await FindForWriteAsync(id, expectedVersion, ct);

        if (!await _db.Statuses.AnyAsync(s => s.Id == newStatusId, ct))
        {
            throw new EntityNotFoundException(nameof(Status), newStatusId);
        }

        var rank = await ComputeRankAsync(item.ParentId, newStatusId, afterId, excludeItemId: id, ct);
        return await SaveWriteAsync(item, item.MoveToStatus(newStatusId, rank), ct);
    }

    public async Task<WorkItemWriteResult> ReparentAsync(
        Guid id,
        Guid? newParentId,
        Guid? afterId = null,
        uint? expectedVersion = null,
        CancellationToken ct = default)
    {
        var item = await FindForWriteAsync(id, expectedVersion, ct);

        if (newParentId is not null)
        {
            if (await _hierarchy.IsSelfOrDescendantAsync(id, newParentId.Value, ct))
            {
                throw new CyclicParentException(id, newParentId.Value);
            }

            if (!await _db.WorkItems.AnyAsync(w => w.Id == newParentId, ct))
            {
                throw new EntityNotFoundException(nameof(WorkItem), newParentId.Value);
            }
        }

        var rank = await ComputeRankAsync(newParentId, item.StatusId, afterId, excludeItemId: id, ct);
        return await SaveWriteAsync(item, item.MoveToParent(newParentId, rank), ct);
    }

    public async Task<WorkItemWriteResult> RescheduleAsync(
        Guid id,
        Optional<DateOnly?> startDate,
        Optional<DateOnly?> endDate,
        uint? expectedVersion = null,
        CancellationToken ct = default)
    {
        var item = await FindForWriteAsync(id, expectedVersion, ct);
        return await SaveWriteAsync(item, item.Reschedule(startDate, endDate), ct);
    }

    public async Task<WorkItemWriteResult> UpdateDetailsAsync(
        Guid id,
        WorkItemDetailsPatch patch,
        uint? expectedVersion = null,
        CancellationToken ct = default)
    {
        await EnsureDetailLookupsExistAsync(patch.LayerId.GetValueOr(null), patch.PriorityId.GetValueOr(null), ct);

        var item = await FindForWriteAsync(id, expectedVersion, ct);
        return await SaveWriteAsync(item, item.UpdateDetails(patch), ct);
    }

    public async Task<WorkItemWriteResult> AssignAsync(
        Guid id,
        Guid? userId,
        uint? expectedVersion = null,
        CancellationToken ct = default)
    {
        if (userId is { } assigneeId)
        {
            var assignee = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == assigneeId, ct)
                ?? throw new EntityNotFoundException(nameof(User), assigneeId);
            if (!assignee.IsActive)
            {
                throw new DomainValidationException($"User '{assignee.Username}' is deactivated and can't be assigned work.");
            }
        }

        var item = await FindForWriteAsync(id, expectedVersion, ct);
        return await SaveWriteAsync(item, item.AssignTo(userId), ct);
    }

    public async Task<WorkItemWriteResult> SetTagsAsync(
        Guid id,
        Optional<IReadOnlyList<string>> tags,
        uint? expectedVersion = null,
        CancellationToken ct = default)
    {
        var item = await FindForWriteAsync(id, expectedVersion, ct);
        var changes = tags.HasValue ? item.SetTags(tags.Value) : [];
        return await SaveWriteAsync(item, changes, ct);
    }

    public async Task DeleteAsync(Guid id, bool cascade = false, uint? expectedVersion = null, CancellationToken ct = default)
    {
        var item = await FindForWriteAsync(id, expectedVersion, ct);

        var descendantIds = await _hierarchy.GetDescendantIdsAsync(id, ct);
        var descendants = await _db.WorkItems.Where(w => descendantIds.Contains(w.Id)).ToListAsync(ct);
        if (descendants.Count > 0 && !cascade)
        {
            throw new WorkItemHasChildrenException(id);
        }

        var deletedIds = descendants.Select(w => w.Id).Append(id).ToList();
        await EnsureNotBoardScopeAsync(id, deletedIds, ct);

        // Link FKs are Restrict (two Cascade paths to WorkItems is rejected by
        // EF), so links touching the deleted subtree must be removed explicitly
        // in the same SaveChanges or Postgres rejects the whole delete.
        var links = await _db.WorkItemLinks
            .Where(l => deletedIds.Contains(l.WorkItemId) || deletedIds.Contains(l.LinkedWorkItemId))
            .ToListAsync(ct);

        _db.WorkItemLinks.RemoveRange(links);
        _db.WorkItems.RemoveRange(descendants);
        _db.WorkItems.Remove(item);
        foreach (var deleted in descendants.Append(item))
        {
            _history.Record(deleted.Id, WorkItemHistoryField.Deleted, deleted.Title, null);
        }
        await SaveWorkItemChangesAsync(id, ct);
    }

    /// <summary>
    /// A board is a saved view other users rely on, so deleting its scope item is rejected
    /// rather than silently deleting the board or widening it to top-level.
    /// </summary>
    private async Task EnsureNotBoardScopeAsync(Guid rootId, IReadOnlyList<Guid> deletedIds, CancellationToken ct)
    {
        var scopedBoardId = await _db.Boards
            .Where(b => b.ScopeItemId != null && deletedIds.Contains(b.ScopeItemId.Value))
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

        if (scopedBoardId is not null)
        {
            throw new WorkItemIsBoardScopeException(rootId, scopedBoardId.Value);
        }
    }

    /// <summary>
    /// Rejects an overwrite based on a stale read. <c>xmin</c> alone only guards the few
    /// milliseconds inside one request; comparing against the version the client last saw
    /// is what stops one user's save from silently discarding another's.
    /// </summary>
    private async Task<WorkItem> FindForWriteAsync(Guid id, uint? expectedVersion, CancellationToken ct)
    {
        var item = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);

        if (expectedVersion is not null && item.Version != expectedVersion)
        {
            throw new WorkItemVersionConflictException(item.Id);
        }

        return item;
    }

    private async Task<WorkItemWriteResult> SaveWriteAsync(
        WorkItem item,
        IReadOnlyList<FieldChange> changes,
        CancellationToken ct)
    {
        _history.RecordChanges(item.Id, changes);
        await SaveWorkItemChangesAsync(item.Id, ct);
        return new WorkItemWriteResult(item, changes.Select(c => c.Field).ToList());
    }

    private async Task EnsureDetailLookupsExistAsync(Guid? layerId, Guid? priorityId, CancellationToken ct)
    {
        if (layerId is not null && !await _db.WorkItemLayers.AnyAsync(l => l.Id == layerId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItemLayer), layerId.Value);
        }

        if (priorityId is not null && !await _db.WorkItemPriorities.AnyAsync(p => p.Id == priorityId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItemPriority), priorityId.Value);
        }
    }

    private async Task SaveWorkItemChangesAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new WorkItemVersionConflictException(id, ex);
        }
    }

    /// <summary>
    /// The rank for an item placed first in its cell, or directly after
    /// <paramref name="afterId"/>. Only the one or two neighbouring rows are read; the whole
    /// cell is loaded only when two neighbours share a rank and have to be respaced.
    /// </summary>
    private async Task<double> ComputeRankAsync(
        Guid? parentId,
        Guid statusId,
        Guid? afterId,
        Guid? excludeItemId,
        CancellationToken ct)
    {
        var cell = CellQuery(parentId, statusId, excludeItemId);

        if (afterId is null)
        {
            var firstRank = await cell.OrderBy(w => w.Rank).ThenBy(w => w.Number)
                .Select(w => (double?)w.Rank)
                .FirstOrDefaultAsync(ct);
            return RankCalculator.GetRankBetween(null, firstRank);
        }

        var afterRank = await cell.Where(w => w.Id == afterId)
            .Select(w => (double?)w.Rank)
            .FirstOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), afterId.Value);

        if (await cell.AnyAsync(w => w.Id != afterId && w.Rank == afterRank, ct))
        {
            return await RespaceAndRankAfterAsync(cell, afterId.Value, ct);
        }

        var nextRank = await cell
            .Where(w => w.Rank > afterRank)
            .OrderBy(w => w.Rank)
            .Select(w => (double?)w.Rank)
            .FirstOrDefaultAsync(ct);

        return RankCalculator.GetRankBetween(afterRank, nextRank);
    }

    private IQueryable<WorkItem> CellQuery(Guid? parentId, Guid statusId, Guid? excludeItemId)
    {
        var query = _db.WorkItems.Where(w => w.ParentId == parentId && w.StatusId == statusId);
        return excludeItemId is null ? query : query.Where(w => w.Id != excludeItemId);
    }

    private async Task<double> RespaceAndRankAfterAsync(IQueryable<WorkItem> cell, Guid afterId, CancellationToken ct)
    {
        var orderedCell = await cell.OrderBy(w => w.Rank).ThenBy(w => w.Number).ThenBy(w => w.Id).ToListAsync(ct);
        RespaceRanks(orderedCell);

        var afterIndex = orderedCell.FindIndex(w => w.Id == afterId);
        var next = afterIndex + 1 < orderedCell.Count ? orderedCell[afterIndex + 1] : null;
        return RankCalculator.GetRankBetween(orderedCell[afterIndex].Rank, next?.Rank);
    }

    /// <summary>
    /// Two siblings sharing a rank leave no gap to insert into. The cell is loaded (and
    /// tracked) for this, so its new ranks are saved with the change that needed them.
    /// </summary>
    private static void RespaceRanks(IReadOnlyList<WorkItem> orderedCell)
    {
        var ranks = RankCalculator.EvenlySpaced(orderedCell.Count);
        for (var i = 0; i < orderedCell.Count; i++)
        {
            orderedCell[i].Reposition(ranks[i]);
        }
    }
}
