using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Deleting a work item moves it and its subtree to the Trash, from which humans and
/// read-write agents can restore it (ADR-0007). Purging it is <see cref="IWorkItemPurgeService"/>.
/// </summary>
public interface IWorkItemTrashService
{
    /// <summary>
    /// Moves a work item and every live descendant to the Trash. If it has live children,
    /// <paramref name="cascade"/> must be true or the delete is rejected, so a subtree is never
    /// dropped by accident. Links and comments are kept, but hidden while either end is
    /// trashed. <paramref name="expectedVersion"/> is checked against the item itself, not its
    /// descendants.
    /// </summary>
    Task MoveToTrashAsync(Guid id, bool cascade = false, uint? expectedVersion = null, CancellationToken ct = default);

    /// <summary>Each delete in the Trash, newest first, optionally only those in one Space.</summary>
    Task<IReadOnlyList<TrashedWorkItem>> ListAsync(Guid? spaceId = null, CancellationToken ct = default);

    /// <summary>
    /// Restores an item that was deleted, with the descendants trashed with it, and returns it.
    /// It goes back under its parent if that still exists, otherwise under its nearest ancestor
    /// that does, otherwise to the top level of its Space. Only the item a delete was made on
    /// can be restored; one that went with an ancestor is rejected, naming that ancestor.
    /// </summary>
    Task<WorkItem> RestoreAsync(Guid id, CancellationToken ct = default);
}
