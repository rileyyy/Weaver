using Weaver.Application.Attachments;

namespace Weaver.Application.Tests;

/// <summary>Declines every file unless told to produce a thumbnail or to throw.</summary>
public sealed class FakeThumbnailGenerator : IThumbnailGenerator
{
    public byte[]? Produces { get; set; }

    public Exception? Throws { get; set; }

    /// <summary>The bytes it was last asked to thumbnail.</summary>
    public byte[]? LastSource { get; private set; }

    public async Task<bool> TryGenerateAsync(Stream source, Stream destination, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct);
        LastSource = buffer.ToArray();

        if (Throws is not null)
        {
            throw Throws;
        }

        if (Produces is null)
        {
            return false;
        }

        await destination.WriteAsync(Produces, ct);
        return true;
    }
}
