using Weaver.Domain;

namespace Weaver.Application.Services;

/// <remarks>
/// Every write after <see cref="CreateAsync"/> takes an optional <c>expectedVersion</c>: the
/// item's <see cref="WorkItem.Version"/> from the caller's last read. When it's given and
/// stale, the write is rejected with a <see cref="Domain.Exceptions.WorkItemVersionConflictException"/>
/// instead of overwriting someone else's change.
/// </remarks>
public interface IWorkItemService
{
    Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Direct children of <paramref name="parentId"/>, or the top-level items of
    /// <paramref name="spaceId"/> when there's no parent, ordered by board-cell rank. One of
    /// the two is required.
    /// </summary>
    Task<IReadOnlyList<WorkItem>> GetChildrenAsync(
        Guid? parentId,
        Guid? spaceId = null,
        CancellationToken ct = default);

    /// <summary>
    /// A board's two levels in one call: the direct children of
    /// <paramref name="scopeItemId"/> (or the top-level items of <paramref name="spaceId"/>
    /// when null) as lanes, each with its own direct children as cards. Two queries regardless
    /// of lane count, instead of one request per lane.
    /// </summary>
    Task<IReadOnlyList<Swimlane>> GetSwimlanesAsync(
        Guid? scopeItemId,
        Guid? spaceId = null,
        CancellationToken ct = default);

    /// <summary>
    /// One page of every work item, in <paramref name="spaceId"/> or in every Space when null,
    /// flat and ordered by rank then number. Used by the Hierarchy and Roadmap views, which
    /// page through all of it to build the full parent/child tree client-side.
    /// <paramref name="limit"/> is 1 to <see cref="WorkItemService.MaxPageSize"/>.
    /// </summary>
    Task<IReadOnlyList<WorkItem>> GetAllAsync(
        int offset,
        int limit,
        Guid? spaceId = null,
        CancellationToken ct = default);

    /// <summary>
    /// A top-level item (no <paramref name="parentId"/>) is created in
    /// <paramref name="spaceId"/>, which it then requires; a child is created in its parent's
    /// Space, and naming a different one is rejected.
    /// </summary>
    Task<WorkItem> CreateAsync(
        string title,
        string? description,
        Guid? parentId,
        Guid statusId,
        Guid? afterId = null,
        Guid? layerId = null,
        Guid? priorityId = null,
        Guid? spaceId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Moves a work item to a different column within its current parent's lane.
    /// Never touches <see cref="WorkItem.ParentId"/> — that is what guarantees a
    /// drag between columns can never reparent the item.
    /// </summary>
    Task<WorkItemWriteResult> ChangeStatusAsync(
        Guid id,
        Guid newStatusId,
        Guid? afterId = null,
        uint? expectedVersion = null,
        CancellationToken ct = default);

    /// <summary>
    /// Moves a work item to a different parent, keeping its current status and Space.
    /// Rejects moves that would make the item its own ancestor, or put it under an item in
    /// another Space.
    /// </summary>
    Task<WorkItemWriteResult> ReparentAsync(
        Guid id,
        Guid? newParentId,
        Guid? afterId = null,
        uint? expectedVersion = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a work item. If it has children, <paramref name="cascade"/> must be
    /// true or the delete is rejected — a subtree is never silently dropped.
    /// Links to any deleted item are removed with it. <paramref name="expectedVersion"/> is
    /// checked against the item itself, not its descendants.
    /// </summary>
    Task DeleteAsync(Guid id, bool cascade = false, uint? expectedVersion = null, CancellationToken ct = default);

    /// <summary>
    /// Sets a work item's scheduled start and/or end, independently of status or parent. A
    /// date left out is kept and <c>null</c> clears it. Rejects a start date after the end
    /// date, judged on the dates the item ends up with.
    /// </summary>
    Task<WorkItemWriteResult> RescheduleAsync(
        Guid id,
        Optional<DateOnly?> startDate,
        Optional<DateOnly?> endDate,
        uint? expectedVersion = null,
        CancellationToken ct = default);

    /// <summary>
    /// Patches a work item's descriptive fields — title, description, layer, priority —
    /// independently of status/parent/schedule/assignee. These fields share no
    /// cross-cutting invariant with each other or with those other operations, so they're
    /// safe to group behind one call unlike e.g. status and parent.
    /// </summary>
    Task<WorkItemWriteResult> UpdateDetailsAsync(
        Guid id,
        WorkItemDetailsPatch patch,
        uint? expectedVersion = null,
        CancellationToken ct = default);

    /// <summary>
    /// Sets or clears who a work item is assigned to, independently of
    /// every other field.
    /// </summary>
    Task<WorkItemWriteResult> AssignAsync(
        Guid id,
        Guid? userId,
        uint? expectedVersion = null,
        CancellationToken ct = default);

    /// <summary>
    /// Replaces a work item's full tag list, independently of every other field, or leaves
    /// it alone when <paramref name="tags"/> is left out. Tags are trimmed and de-duplicated
    /// case-insensitively; an empty/blank tag is rejected.
    /// </summary>
    Task<WorkItemWriteResult> SetTagsAsync(
        Guid id,
        Optional<IReadOnlyList<string>> tags,
        uint? expectedVersion = null,
        CancellationToken ct = default);
}
