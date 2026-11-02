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
}
