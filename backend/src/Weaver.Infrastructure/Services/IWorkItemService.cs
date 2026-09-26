using Weaver.Domain;

namespace Weaver.Infrastructure.Services;

public interface IWorkItemService
{
    Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Direct children of <paramref name="parentId"/> (or top-level items when null),
    /// ordered by board-cell rank. The board uses this twice: once for swimlanes
    /// (children of the board's scope item), once per swimlane for its cards.
    /// </summary>
    Task<IReadOnlyList<WorkItem>> GetChildrenAsync(Guid? parentId, CancellationToken ct = default);

    /// <summary>
    /// Every work item in the system, flat and unscoped, ordered by rank.
    /// Used by the Hierarchy view to build a full parent/child tree
    /// client-side from each item's <see cref="WorkItem.ParentId"/> — unlike
    /// <see cref="GetChildrenAsync"/>, this walks no single parent's subtree.
    /// Not paginated; revisit if item counts grow large enough to need it.
    /// </summary>
    Task<IReadOnlyList<WorkItem>> GetAllAsync(CancellationToken ct = default);

    Task<WorkItem> CreateAsync(
        string title,
        string? description,
        Guid? parentId,
        Guid statusId,
        Guid? afterId = null,
        Guid? layerId = null,
        WorkItemPriority priority = WorkItemPriority.Medium,
        CancellationToken ct = default);

    /// <summary>
    /// Moves a work item to a different column within its current parent's lane.
    /// Never touches <see cref="WorkItem.ParentId"/> — that is what guarantees a
    /// drag between columns can never reparent the item.
    /// </summary>
    Task<WorkItem> ChangeStatusAsync(
        Guid id,
        Guid newStatusId,
        Guid? afterId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Moves a work item to a different parent, keeping its current status.
    /// Rejects moves that would make the item its own ancestor.
    /// </summary>
    Task<WorkItem> ReparentAsync(
        Guid id,
        Guid? newParentId,
        Guid? afterId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a work item. If it has children, <paramref name="cascade"/> must be
    /// true or the delete is rejected — a subtree is never silently dropped.
    /// Links to any deleted item are removed with it. The delete is rejected if any
    /// deleted item is a board's scope.
    /// </summary>
    Task DeleteAsync(Guid id, bool cascade = false, CancellationToken ct = default);

    /// <summary>
    /// Sets a work item's scheduled start/end, independently of status or
    /// parent. Either may be null. Rejects a start date after the end date.
    /// Rejects the change if <paramref name="expectedVersion"/> is given and stale.
    /// </summary>
    Task<WorkItem> RescheduleAsync(
        Guid id,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        uint? expectedVersion = null,
        CancellationToken ct = default);

    /// <summary>
    /// Updates a work item's descriptive fields — title, description, layer,
    /// priority — independently of status/parent/schedule/assignee. These
    /// fields share no cross-cutting invariant with each other or with those
    /// other operations, so they're safe to group behind one call unlike
    /// e.g. status and parent. Rejects the change if <paramref name="expectedVersion"/>
    /// is given and stale.
    /// </summary>
    Task<WorkItem> UpdateDetailsAsync(
        Guid id,
        string title,
        string? description,
        Guid? layerId,
        WorkItemPriority priority,
        uint? expectedVersion = null,
        CancellationToken ct = default);

    /// <summary>
    /// Sets or clears who a work item is assigned to, independently of
    /// every other field.
    /// </summary>
    Task<WorkItem> AssignAsync(Guid id, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Replaces a work item's full tag list, independently of every other
    /// field. Tags are trimmed and de-duplicated case-insensitively; an
    /// empty/blank tag is rejected. Rejects the change if <paramref name="expectedVersion"/>
    /// is given and stale.
    /// </summary>
    Task<WorkItem> SetTagsAsync(
        Guid id,
        IReadOnlyList<string> tags,
        uint? expectedVersion = null,
        CancellationToken ct = default);
}
