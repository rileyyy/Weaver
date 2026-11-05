using Weaver.Domain.Exceptions;

namespace Weaver.Application.Attachments;

/// <summary>
/// Reads through to the inner stream and throws <see cref="AttachmentTooLargeException"/> as
/// soon as more than the limit has come through, so an oversized upload is refused one buffer
/// past the limit rather than after the whole body has been read.
/// </summary>
internal sealed class SizeLimitedStream(Stream inner, long maxBytes, int maxMegabytes) : Stream
{
    private readonly Stream _inner = inner;
    private readonly long _maxBytes = maxBytes;
    private readonly int _maxMegabytes = maxMegabytes;

    public long BytesRead { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Counted(_inner.Read(buffer, offset, count));

    public override int Read(Span<byte> buffer) => Counted(_inner.Read(buffer));

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Counted(await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Counted(await _inner.ReadAsync(buffer, cancellationToken));

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int Counted(int read)
    {
        BytesRead += read;
        if (BytesRead > _maxBytes)
        {
            throw new AttachmentTooLargeException(_maxMegabytes);
        }

        return read;
    }
}
