namespace Weaver.Application.Services;

/// <summary>
/// Reads a work item's change history. There is deliberately nothing here (or anywhere) that
/// edits or deletes an entry: the services that change work items append them.
/// </summary>
public interface IWorkItemHistoryService
{
    /// <summary>
    /// Every entry for the item, newest first. Works for a deleted item too, since its history
    /// outlives it; throws <see cref="Weaver.Domain.Exceptions.EntityNotFoundException"/> only
    /// when the item neither exists nor ever did.
    /// </summary>
    Task<IReadOnlyList<WorkItemHistoryView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default);

    /// <summary>
    /// The same entries as <see cref="ListForWorkItemAsync"/>, one page at a time, for agents
    /// that only want the latest few. <paramref name="limit"/> is 1 to
    /// <see cref="WorkItemHistoryService.MaxPageSize"/>.
    /// </summary>
    Task<WorkItemHistoryPage> ListPageForWorkItemAsync(Guid workItemId, int offset, int limit, CancellationToken ct = default);
}
