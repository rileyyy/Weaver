using Weaver.Application.Attachments;

namespace Weaver.Application.Tests;

/// <summary>Holds attachment files in memory, and can be told to fail a write part-way.</summary>
public sealed class InMemoryAttachmentStore : IAttachmentStore
{
    private readonly Dictionary<Guid, byte[]> _files = [];

    public bool FailWrites { get; set; }

    public IReadOnlyDictionary<Guid, byte[]> Files => _files;

    public async Task WriteAsync(Guid id, Stream content, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (FailWrites)
        {
            throw new IOException("Disk full.");
        }

        _files[id] = buffer.ToArray();
    }

    public Task<Stream> OpenReadAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream(_files[id], writable: false));

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _files.Remove(id);
        return Task.CompletedTask;
    }
}
