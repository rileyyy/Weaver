namespace Weaver.Application.Attachments;

/// <summary>
/// The seam <see cref="Services.IWorkItemPurgeService"/> runs through so purged items don't leave
/// their files behind. The attachment rows go with the items; only the files need this.
/// </summary>
public interface IAttachmentFileCleanup
{
    /// <summary>
    /// Runs <paramref name="purgeRows"/>, which must delete and commit
    /// <paramref name="workItemIds"/>, then deletes the files of their attachments, trashed or
    /// not. The files are found before the rows go and removed only once the purge has
    /// committed, so a failed purge leaves every trashed item with its files (it can still be
    /// restored whole); the worst a failure here can leave is an unreferenced file on disk.
    /// </summary>
    Task PurgeAsync(
        IReadOnlyCollection<Guid> workItemIds,
        Func<CancellationToken, Task> purgeRows,
        CancellationToken ct = default);
}
