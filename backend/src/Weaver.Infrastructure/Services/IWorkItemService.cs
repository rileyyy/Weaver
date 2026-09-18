using Weaver.Domain;

namespace Weaver.Infrastructure.Services;

public interface IWorkItemService
{
    Task<WorkItem> CreateAsync(
        string title,
        string? description,
        Guid? parentId,
        Guid statusId,
        Guid? afterId = null,
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
    /// </summary>
    Task DeleteAsync(Guid id, bool cascade = false, CancellationToken ct = default);
}
