namespace Weaver.Application.Attachments;

/// <summary>
/// Where attachment files live, each under its attachment's id, with an image's thumbnail kept
/// beside it under the same id. Kept apart from the database so the services can be tested
/// without a disk, and the files can move to other storage.
/// </summary>
public interface IAttachmentStore
{
    /// <summary>
    /// Writes <paramref name="content"/> to its end. If writing fails part-way (including the
    /// content stream throwing), no file is left under <paramref name="id"/>.
    /// </summary>
    Task WriteAsync(Guid id, Stream content, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(Guid id, CancellationToken ct = default);

    /// <summary>As <see cref="WriteAsync"/>, for the attachment's thumbnail.</summary>
    Task WriteThumbnailAsync(Guid id, Stream content, CancellationToken ct = default);

    Task<Stream> OpenThumbnailAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Removes the file and its thumbnail, so deleting or purging an attachment needs nothing
    /// else. Either being already gone (or never having had a thumbnail) is not an error.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
