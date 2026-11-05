namespace Weaver.Application.Attachments;

/// <summary>
/// The seam purging (ticket 20) calls so purged items don't leave their files behind. The
/// attachment rows go with the items by FK cascade; only the files need this.
/// </summary>
public interface IAttachmentFileCleanup
{
    /// <summary>
    /// Deletes the files of every attachment on <paramref name="workItemIds"/>, trashed or not.
    /// Call it before deleting the items: their attachment rows are how the files are found.
    /// If the purge then fails, the items have lost their files but were being purged anyway,
    /// and a retry finishes the job.
    /// </summary>
    Task DeleteFilesForWorkItemsAsync(IReadOnlyCollection<Guid> workItemIds, CancellationToken ct = default);
}
