using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemSpaceMoveService(
    IWeaverDbContext db,
    IWorkItemHierarchy hierarchy,
    ICallerContext caller) : IWorkItemSpaceMoveService
{
    private readonly IWeaverDbContext _db = db;
    private readonly IWorkItemHierarchy _hierarchy = hierarchy;
    private readonly WorkItemHistoryRecorder _history = new WorkItemHistoryRecorder(db, caller);

    public async Task<SpaceMovePlan> PlanAsync(Guid id, Guid targetSpaceId, CancellationToken ct = default)
    {
        var item = await FindLiveAsync(_db.WorkItems.AsNoTracking(), id, ct);
        await _db.EnsureSpaceExistsAsync(targetSpaceId, ct);
        item.EnsureCanMoveToSpace(targetSpaceId, parent: null);

        var descendants = await LoadDescendantsAsync(_db.WorkItems.IgnoreQueryFilters().AsNoTracking(), id, ct);
        var mapper = await SpaceValueMapper.LoadAsync(_db, item.SpaceId, targetSpaceId, SpaceMoveMaps.AllDefaults, ct);
        return mapper.Plan(id, targetSpaceId, [item, .. descendants]);
    }

    public async Task<WorkItemWriteResult> MoveAsync(
        Guid id,
        Guid targetSpaceId,
        Guid? targetParentId,
        SpaceMoveMaps maps,
        uint? expectedVersion = null,
        CancellationToken ct = default)
    {
        var item = await FindLiveAsync(_db.WorkItems, id, ct);
        if (expectedVersion is not null && item.Version != expectedVersion)
        {
            throw new WorkItemVersionConflictException(item.Id);
        }

        await _db.EnsureSpaceExistsAsync(targetSpaceId, ct);
        var parent = targetParentId is { } parentId ? await FindLiveAsync(_db.WorkItems.AsNoTracking(), parentId, ct) : null;
        item.EnsureCanMoveToSpace(targetSpaceId, parent);

        var mapper = await SpaceValueMapper.LoadAsync(_db, item.SpaceId, targetSpaceId, maps, ct);
        var rootValues = mapper.Map(item);
        var rank = await FirstRankInCellAsync(targetSpaceId, targetParentId, rootValues.StatusId, ct);
        var rootChanges = item.MoveToSpace(targetSpaceId, parent, rank, rootValues);
        _history.RecordChanges(item.Id, rootChanges);

        var descendants = await LoadDescendantsAsync(_db.WorkItems.IgnoreQueryFilters(), id, ct);
        MoveDescendants(item, descendants, mapper);
        await _db.SaveWorkItemChangesAsync(id, ct);
        return new WorkItemWriteResult(item, rootChanges.Select(c => c.Field).ToList());
    }

    /// <summary>
    /// Descendants in the Trash are included: restoring one puts it back under its parent, so it
    /// has to be in its parent's Space.
    /// </summary>
    private async Task<List<WorkItem>> LoadDescendantsAsync(IQueryable<WorkItem> items, Guid rootId, CancellationToken ct)
    {
        var descendantIds = await _hierarchy.GetDescendantIdsIncludingTrashAsync(rootId, ct);
        return await items.Where(w => descendantIds.Contains(w.Id)).ToListAsync(ct);
    }

    /// <summary>Parents go before their children, since each follows its parent's new Space.</summary>
    private void MoveDescendants(WorkItem root, IEnumerable<WorkItem> descendants, SpaceValueMapper mapper)
    {
        var childrenByParent = descendants.ToLookup(w => w.ParentId);
        var parents = new Queue<WorkItem>([root]);
        while (parents.TryDequeue(out var parent))
        {
            foreach (var child in childrenByParent[parent.Id])
            {
                _history.RecordChanges(child.Id, child.FollowParentToSpace(parent, mapper.Map(child)));
                parents.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// The same place a reparent with no sibling to follow puts an item. Only the lowest rank
    /// matters here, so unlike a cell's listing order there's no tie to break.
    /// </summary>
    private async Task<double> FirstRankInCellAsync(Guid spaceId, Guid? parentId, Guid statusId, CancellationToken ct)
    {
        var firstRank = await _db.WorkItems
            .Where(w => w.SpaceId == spaceId && w.ParentId == parentId && w.StatusId == statusId)
            .Select(w => (double?)w.Rank)
            .MinAsync(ct);
        return RankCalculator.GetRankBetween(null, firstRank);
    }

    private static async Task<WorkItem> FindLiveAsync(IQueryable<WorkItem> items, Guid id, CancellationToken ct) =>
        await items.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);
}
