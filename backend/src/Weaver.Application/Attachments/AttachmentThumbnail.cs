namespace Weaver.Application.Attachments;

/// <summary>What every attachment thumbnail is, whichever <see cref="IThumbnailGenerator"/> made it.</summary>
public static class AttachmentThumbnail
{
    /// <summary>WebP keeps transparency, unlike JPEG, and is far smaller than PNG for photos.</summary>
    public const string ContentType = "image/webp";

    /// <summary>The longest side, in pixels. A smaller image keeps its size.</summary>
    public const int MaxEdge = 320;
}
