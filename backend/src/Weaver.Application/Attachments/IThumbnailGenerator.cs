namespace Weaver.Application.Attachments;

/// <summary>Makes the small preview shown for an image attachment in place of an icon.</summary>
public interface IThumbnailGenerator
{
    /// <summary>
    /// Reads <paramref name="source"/> and, if it's an image this can thumbnail, writes the
    /// thumbnail to <paramref name="destination"/> and returns true. Returns false, writing
    /// nothing, for anything that isn't a supported image or is too large to decode safely.
    /// May throw for a file that looks like an image but can't be decoded.
    /// </summary>
    Task<bool> TryGenerateAsync(Stream source, Stream destination, CancellationToken ct = default);
}
