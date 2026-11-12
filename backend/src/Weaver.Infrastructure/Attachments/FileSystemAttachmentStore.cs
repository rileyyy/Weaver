using Microsoft.Extensions.Options;
using Weaver.Application.Attachments;

namespace Weaver.Infrastructure.Attachments;

/// <summary>
/// Keeps each attachment as one file, named by its id, directly under the configured path, and
/// its thumbnail (if any) beside it as <c>{id}.thumbnail</c>.
/// </summary>
public sealed class FileSystemAttachmentStore(IOptions<AttachmentOptions> options) : IAttachmentStore
{
    private const int BufferSize = 81920;
    private const string ThumbnailSuffix = ".thumbnail";

    private readonly string _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Value.Path));

    public void EnsureRootExists() => Directory.CreateDirectory(_root);

    public Task WriteAsync(Guid id, Stream content, CancellationToken ct = default) =>
        WriteFileAsync(PathFor(id), content, ct);

    public Task<Stream> OpenReadAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(OpenFile(PathFor(id)));

    public Task WriteThumbnailAsync(Guid id, Stream content, CancellationToken ct = default) =>
        WriteFileAsync(ThumbnailPathFor(id), content, ct);

    public Task<Stream> OpenThumbnailAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(OpenFile(ThumbnailPathFor(id)));

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        File.Delete(PathFor(id));
        File.Delete(ThumbnailPathFor(id));
        return Task.CompletedTask;
    }

    private static async Task WriteFileAsync(string path, Stream content, CancellationToken ct)
    {
        // Written aside and moved into place, so a reader (or a crash) never sees half a file
        // under the attachment's own name.
        var partial = path + ".partial";
        try
        {
            await using (var file = new FileStream(
                partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous))
            {
                await content.CopyToAsync(file, BufferSize, ct);
            }

            File.Move(partial, path);
        }
        catch
        {
            File.Delete(partial);
            throw;
        }
    }

    private static Stream OpenFile(string path) => new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        BufferSize,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private string ThumbnailPathFor(Guid id) => PathFor(id) + ThumbnailSuffix;

    /// <summary>
    /// A Guid can't hold a separator, so this can't leave the root; the check makes that a rule
    /// rather than an accident of the id's type.
    /// </summary>
    private string PathFor(Guid id)
    {
        var path = Path.GetFullPath(Path.Combine(_root, id.ToString("D")));
        if (!string.Equals(Path.GetDirectoryName(path), _root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("An attachment's file must be directly under the attachments path.");
        }

        return path;
    }
}
