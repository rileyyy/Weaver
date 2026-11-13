using Weaver.Application.Attachments;

namespace Weaver.Application.Tests;

/// <summary>Holds attachment files and thumbnails in memory, and can be told to fail a write part-way.</summary>
public sealed class InMemoryAttachmentStore : IAttachmentStore
{
    private readonly Dictionary<Guid, byte[]> _files = [];
    private readonly Dictionary<Guid, byte[]> _thumbnails = [];

    public bool FailWrites { get; set; }

    public bool FailThumbnailWrites { get; set; }

    public IReadOnlyDictionary<Guid, byte[]> Files => _files;

    public IReadOnlyDictionary<Guid, byte[]> Thumbnails => _thumbnails;

    public Task WriteAsync(Guid id, Stream content, CancellationToken ct = default) =>
        WriteToAsync(_files, FailWrites, id, content, ct);

    public Task<Stream> OpenReadAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream(_files[id], writable: false));

    public Task WriteThumbnailAsync(Guid id, Stream content, CancellationToken ct = default) =>
        WriteToAsync(_thumbnails, FailThumbnailWrites, id, content, ct);

    public Task<Stream> OpenThumbnailAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream(_thumbnails[id], writable: false));

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _files.Remove(id);
        _thumbnails.Remove(id);
        return Task.CompletedTask;
    }

    private static async Task WriteToAsync(
        Dictionary<Guid, byte[]> files,
        bool fail,
        Guid id,
        Stream content,
        CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (fail)
        {
            throw new IOException("Disk full.");
        }

        files[id] = buffer.ToArray();
    }
}
