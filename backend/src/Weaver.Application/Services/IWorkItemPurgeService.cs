namespace Weaver.Application.Services;

/// <summary>
/// Purging removes deletes from the Trash for good (ADR-0007): by hand, by a human, or by the
/// background job once a delete has been in the Trash longer than the server's retention
/// period. A delete goes whole: the item it was made on and everything trashed with it.
/// </summary>
public interface IWorkItemPurgeService
{
    /// <summary>
    /// Purges the delete made on <paramref name="id"/>. Only the item a delete was made on can
    /// be purged; one that went with an ancestor is rejected, naming that ancestor. Agent
    /// accounts are refused, whatever their key's scope.
    /// </summary>
    Task PurgeAsync(Guid id, CancellationToken ct = default);

    /// <summary>Purges every delete in the Trash, in every Space. Agent accounts are refused.</summary>
    Task EmptyTrashAsync(CancellationToken ct = default);

    /// <summary>
    /// Purges every delete that has been in the Trash for the retention period or longer, and
    /// returns how many work items went. Recorded as Weaver itself, so it needs no caller.
    /// </summary>
    Task<int> PurgeExpiredAsync(CancellationToken ct = default);
}
