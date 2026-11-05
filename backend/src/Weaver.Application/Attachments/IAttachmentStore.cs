namespace Weaver.Application.Attachments;

/// <summary>
/// Where attachment files live, each under its attachment's id. Kept apart from the database
/// so the services can be tested without a disk, and the files can move to other storage.
/// </summary>
public interface IAttachmentStore
{
    /// <summary>
    /// Writes <paramref name="content"/> to its end. If writing fails part-way (including the
    /// content stream throwing), no file is left under <paramref name="id"/>.
    /// </summary>
    Task WriteAsync(Guid id, Stream content, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(Guid id, CancellationToken ct = default);

    /// <summary>A file that's already gone is not an error.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
