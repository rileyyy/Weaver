using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Weaver.Application.Attachments;

namespace Weaver.Infrastructure.Attachments;

/// <summary>
/// Thumbnails JPEG, PNG, GIF, WebP and BMP images, recognised by their content rather than the
/// uploader's content type. SVG isn't among them, so a script-carrying file is never rendered here.
/// </summary>
public sealed class ImageSharpThumbnailGenerator : IThumbnailGenerator
{
    public const int MaxEdge = 320;

    /// <summary>
    /// About a 50 megapixel photo. A header can claim far more than the file holds, and decoding
    /// allocates for every claimed pixel, so anything bigger is left without a thumbnail.
    /// </summary>
    public const long MaxSourcePixels = 50_000_000;

    /// <summary>
    /// Only the formats people attach as pictures. TIFF and the rest are left out because every
    /// decoder is attack surface for untrusted uploads, and ImageSharp 3.1's TIFF decoder has
    /// known vulnerabilities (see Directory.Packages.props).
    /// </summary>
    private static readonly Configuration Formats = new(
        new JpegConfigurationModule(),
        new PngConfigurationModule(),
        new GifConfigurationModule(),
        new WebpConfigurationModule(),
        new BmpConfigurationModule());

    // WebP keeps transparency, unlike JPEG, and is far smaller than PNG for photos.
    private static readonly WebpEncoder Encoder = new() { FileFormat = WebpFileFormatType.Lossy, Quality = 80 };

    public async Task<bool> TryGenerateAsync(Stream source, Stream destination, CancellationToken ct = default)
    {
        var seekable = await SeekableAsync(source, ct);
        var start = seekable.Position;

        ImageInfo info;
        try
        {
            info = await Image.IdentifyAsync(new DecoderOptions { Configuration = Formats }, seekable, ct);
        }
        catch (UnknownImageFormatException)
        {
            return false;
        }

        if ((long)info.Width * info.Height > MaxSourcePixels)
        {
            return false;
        }

        seekable.Position = start;
        using var image = await Image.LoadAsync(DecoderOptionsFor(info), seekable, ct);
        image.Mutate(i => i.AutoOrient());
        if (image.Width > MaxEdge || image.Height > MaxEdge)
        {
            image.Mutate(i => i.Resize(new ResizeOptions { Size = new Size(MaxEdge, MaxEdge), Mode = ResizeMode.Max }));
        }

        // The thumbnail is served to anyone who can see the item, and a photo's EXIF can hold where
        // it was taken. The ICC profile goes too, so its parser (which has a known allocation bug in
        // ImageSharp 3.1) is never reached.
        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.IccProfile = null;

        await image.SaveAsync(destination, Encoder, ct);
        return true;
    }

    /// <summary>
    /// One frame is enough for a still preview. A target size lets the JPEG decoder decode a big
    /// photo straight at a fraction of its size, rather than in full and then shrunk.
    /// </summary>
    private static DecoderOptions DecoderOptionsFor(ImageInfo info) => new()
    {
        Configuration = Formats,
        MaxFrames = 1,
        TargetSize = info.Width > MaxEdge || info.Height > MaxEdge ? new Size(MaxEdge, MaxEdge) : null,
    };

    /// <summary>Identifying and then decoding reads the start twice.</summary>
    private static async Task<Stream> SeekableAsync(Stream source, CancellationToken ct)
    {
        if (source.CanSeek)
        {
            return source;
        }

        var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return buffer;
    }
}
