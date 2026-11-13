namespace Weaver.Application.Services;

/// <summary>Files attached to work items. An attachment on a trashed item is kept, but hidden with it.</summary>
public interface IWorkItemAttachmentService
{
    /// <summary>The item's attachments, oldest first.</summary>
    Task<IReadOnlyList<WorkItemAttachmentView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default);

    /// <summary>
    /// Streams <paramref name="content"/> to storage and records the attachment. Throws
    /// <see cref="Weaver.Domain.Exceptions.AttachmentTooLargeException"/> once more than the
    /// configured maximum has been read, and
    /// <see cref="Weaver.Domain.Exceptions.DomainValidationException"/> for an empty file.
    /// A failed add leaves neither a file nor a row behind. An image the server can decode gets a
    /// thumbnail; failing to make one never fails the add, the attachment just has none.
    /// </summary>
    Task<WorkItemAttachmentView> AddAsync(
        Guid workItemId,
        string? fileName,
        string? contentType,
        Stream content,
        CancellationToken ct = default);

    Task<AttachmentContent> OpenContentAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The attachment's thumbnail, open for reading; the caller disposes it. Throws
    /// <see cref="Weaver.Domain.Exceptions.AttachmentThumbnailNotFoundException"/> when it has none.
    /// </summary>
    Task<Stream> OpenThumbnailAsync(Guid id, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
