namespace Weaver.Application.Services;

/// <summary>
/// Which live items block which work items, one query per batch of items. Readers use it to
/// resolve <see cref="Weaver.Domain.WorkItem.IsWhenResolved"/> (an item is blocked when it has
/// any blocker) and to flag dependency conflicts.
/// </summary>
public interface IWorkItemBlockers
{
    /// <summary>
    /// The live blockers of each item in <paramref name="workItemIds"/>, Done ones included, by
    /// Number. An item nothing blocks is left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<WorkItemBlocker>>> BlockersOfAsync(
        IReadOnlyCollection<Guid> workItemIds,
        CancellationToken ct = default);
}
