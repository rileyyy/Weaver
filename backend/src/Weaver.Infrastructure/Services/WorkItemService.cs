using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Infrastructure.Services;

public class WorkItemService : IWorkItemService
{
    private readonly WeaverDbContext _db;

    public WorkItemService(WeaverDbContext db)
    {
        _db = db;
    }

    public Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.WorkItems.FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<IReadOnlyList<WorkItem>> GetChildrenAsync(Guid? parentId, CancellationToken ct = default) =>
        await _db.WorkItems
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

    public async Task<IReadOnlyList<WorkItem>> GetAllAsync(CancellationToken ct = default) =>
        await _db.WorkItems
            .OrderBy(w => w.Rank)
            .ThenBy(w => w.Number)
            .ToListAsync(ct);

    public async Task<WorkItem> CreateAsync(
        string title,
        string? description,
        Guid? parentId,
        Guid statusId,
        Guid? afterId = null,
        Guid? layerId = null,
        WorkItemPriority priority = WorkItemPriority.Medium,
        CancellationToken ct = default)
    {
        TextValidation.RequireText(title, "Title", WorkItem.TitleMaxLength);

        if (parentId is not null && !await _db.WorkItems.AnyAsync(w => w.Id == parentId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), parentId.Value);
        }

        if (!await _db.Statuses.AnyAsync(s => s.Id == statusId, ct))
        {
            throw new EntityNotFoundException(nameof(Status), statusId);
        }

        if (layerId is not null && !await _db.WorkItemLayers.AnyAsync(l => l.Id == layerId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItemLayer), layerId.Value);
        }

        var now = DateTimeOffset.UtcNow;
        var item = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            ParentId = parentId,
            StatusId = statusId,
            LayerId = layerId,
            Priority = priority,
            Rank = await ComputeRankAsync(parentId, statusId, afterId, excludeItemId: null, ct),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _db.WorkItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<WorkItem> ChangeStatusAsync(
        Guid id,
        Guid newStatusId,
        Guid? afterId = null,
        CancellationToken ct = default)
    {
        var item = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);

        if (!await _db.Statuses.AnyAsync(s => s.Id == newStatusId, ct))
        {
            throw new EntityNotFoundException(nameof(Status), newStatusId);
        }

        item.Rank = await ComputeRankAsync(item.ParentId, newStatusId, afterId, excludeItemId: id, ct);
        item.StatusId = newStatusId;
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await SaveWorkItemChangesAsync(id, ct);
        return item;
    }

    public async Task<WorkItem> ReparentAsync(
        Guid id,
        Guid? newParentId,
        Guid? afterId = null,
        CancellationToken ct = default)
    {
        var item = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);

        if (newParentId is not null)
        {
            if (await WouldCreateCycleAsync(id, newParentId.Value, ct))
            {
                throw new CyclicParentException(id, newParentId.Value);
            }

            if (!await _db.WorkItems.AnyAsync(w => w.Id == newParentId, ct))
            {
                throw new EntityNotFoundException(nameof(WorkItem), newParentId.Value);
            }
        }

        item.Rank = await ComputeRankAsync(newParentId, item.StatusId, afterId, excludeItemId: id, ct);
        item.ParentId = newParentId;
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await SaveWorkItemChangesAsync(id, ct);
        return item;
    }

    public async Task<WorkItem> RescheduleAsync(
        Guid id,
        DateOnly? startDate,
        DateOnly? endDate,
        uint? expectedVersion = null,
        CancellationToken ct = default)
    {
        if (startDate is not null && endDate is not null && startDate > endDate)
        {
            throw new InvalidWorkItemScheduleException(id);
        }

        var item = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);

        EnsureVersion(item, expectedVersion);

        item.StartDate = startDate;
        item.EndDate = endDate;
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await SaveWorkItemChangesAsync(id, ct);
        return item;
    }

    public async Task<WorkItem> UpdateDetailsAsync(
        Guid id,
        string title,
        string? description,
        Guid? layerId,
        WorkItemPriority priority,
        uint? expectedVersion = null,
        CancellationToken ct = default)
    {
        TextValidation.RequireText(title, "Title", WorkItem.TitleMaxLength);

        if (layerId is not null && !await _db.WorkItemLayers.AnyAsync(l => l.Id == layerId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItemLayer), layerId.Value);
        }

        var item = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);

        EnsureVersion(item, expectedVersion);

        item.Title = title;
        item.Description = description;
        item.LayerId = layerId;
        item.Priority = priority;
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await SaveWorkItemChangesAsync(id, ct);
        return item;
    }

    public async Task<WorkItem> AssignAsync(Guid id, Guid? userId, CancellationToken ct = default)
    {
        if (userId is not null && !await _db.Users.AnyAsync(u => u.Id == userId, ct))
        {
            throw new EntityNotFoundException(nameof(User), userId.Value);
        }

        var item = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);

        item.AssignedToUserId = userId;
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await SaveWorkItemChangesAsync(id, ct);
        return item;
    }

    public async Task<WorkItem> SetTagsAsync(
        Guid id,
        IReadOnlyList<string> tags,
        uint? expectedVersion = null,
        CancellationToken ct = default)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            var trimmed = tag.Trim();
            if (trimmed.Length == 0)
            {
                throw new InvalidWorkItemTagException(id);
            }

            TextValidation.RequireMaxLength(trimmed, "Each tag", WorkItem.TagMaxLength);

            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        if (normalized.Count > WorkItem.MaxTags)
        {
            throw new DomainValidationException($"A work item can have at most {WorkItem.MaxTags} tags.");
        }

        var item = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);

        EnsureVersion(item, expectedVersion);

        item.Tags = normalized;
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await SaveWorkItemChangesAsync(id, ct);
        return item;
    }

    public async Task DeleteAsync(Guid id, bool cascade = false, CancellationToken ct = default)
    {
        var item = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);

        var descendants = await LoadDescendantsAsync(id, ct);
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
        await _db.SaveChangesAsync(ct);
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
    private static void EnsureVersion(WorkItem item, uint? expectedVersion)
    {
        if (expectedVersion is not null && item.Version != expectedVersion)
        {
            throw new WorkItemVersionConflictException(item.Id);
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
    /// True if setting <paramref name="itemId"/>'s parent to <paramref name="proposedParentId"/>
    /// would create a cycle — i.e. <paramref name="proposedParentId"/> is <paramref name="itemId"/>
    /// itself or one of its descendants. Checked by walking upward from the proposed parent
    /// through its ancestor chain one link at a time (rather than a single recursive query) so
    /// it works identically against any EF Core provider, including the in-memory one used in
    /// tests.
    /// </summary>
    private async Task<bool> WouldCreateCycleAsync(Guid itemId, Guid proposedParentId, CancellationToken ct)
    {
        Guid? currentId = proposedParentId;
        var visited = new HashSet<Guid>();

        while (currentId is not null)
        {
            if (currentId == itemId)
            {
                return true;
            }

            if (!visited.Add(currentId.Value))
            {
                return false;
            }

            currentId = await _db.WorkItems
                .Where(w => w.Id == currentId)
                .Select(w => w.ParentId)
                .FirstOrDefaultAsync(ct);
        }

        return false;
    }

    private async Task<List<WorkItem>> LoadDescendantsAsync(Guid rootId, CancellationToken ct)
    {
        var descendants = new List<WorkItem>();
        var frontierIds = new List<Guid> { rootId };

        while (frontierIds.Count > 0)
        {
            var children = await _db.WorkItems
                .Where(w => w.ParentId != null && frontierIds.Contains(w.ParentId.Value))
                .ToListAsync(ct);

            descendants.AddRange(children);
            frontierIds = children.Select(w => w.Id).ToList();
        }

        return descendants;
    }

    private async Task<double> ComputeRankAsync(
        Guid? parentId,
        Guid statusId,
        Guid? afterId,
        Guid? excludeItemId,
        CancellationToken ct)
    {
        var query = _db.WorkItems.Where(w => w.ParentId == parentId && w.StatusId == statusId);
        if (excludeItemId is not null)
        {
            query = query.Where(w => w.Id != excludeItemId);
        }

        var cellItems = await query.OrderBy(w => w.Rank).ToListAsync(ct);

        if (afterId is null)
        {
            return RankCalculator.GetRankBetween(null, cellItems.FirstOrDefault()?.Rank);
        }

        var afterIndex = cellItems.FindIndex(w => w.Id == afterId.Value);
        if (afterIndex < 0)
        {
            throw new EntityNotFoundException(nameof(WorkItem), afterId.Value);
        }

        var next = afterIndex + 1 < cellItems.Count ? cellItems[afterIndex + 1] : null;
        return RankCalculator.GetRankBetween(cellItems[afterIndex].Rank, next?.Rank);
    }
}
