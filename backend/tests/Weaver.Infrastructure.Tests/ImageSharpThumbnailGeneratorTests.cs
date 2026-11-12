using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Weaver.Infrastructure.Attachments;

namespace Weaver.Infrastructure.Tests;

[TestFixture]
public class ImageSharpThumbnailGeneratorTests
{
    private readonly ImageSharpThumbnailGenerator _generator = new();

    [Test]
    public async Task APngWithTransparency_BecomesAWebpThatKeepsIt()
    {
        using var source = new Image<Rgba32>(100, 50, new Rgba32(255, 0, 0, 255));
        for (var x = 0; x < 50; x++)
        {
            for (var y = 0; y < 50; y++)
            {
                source[x, y] = new Rgba32(0, 0, 0, 0);
            }
        }

        var (generated, thumbnail) = await GenerateAsync(await EncodeAsync(source, (image, s) => image.SaveAsPngAsync(s)));

        using var decoded = Image.Load<Rgba32>(thumbnail);
        Assert.Multiple(() =>
        {
            Assert.That(generated, Is.True);
            Assert.That(Image.DetectFormat(thumbnail), Is.EqualTo(WebpFormat.Instance));
            Assert.That(decoded[10, 25].A, Is.Zero);
            Assert.That(decoded[90, 25].A, Is.EqualTo(255));
        });
    }

    [Test]
    public async Task ASmallImage_KeepsItsSize()
    {
        using var source = new Image<Rgba32>(100, 50);

        var (_, thumbnail) = await GenerateAsync(await EncodeAsync(source, (image, s) => image.SaveAsPngAsync(s)));

        Assert.That(SizeOf(thumbnail), Is.EqualTo(new Size(100, 50)));
    }

    [TestCase(1000, 500, 320, 160)]
    [TestCase(300, 1200, 80, 320)]
    [TestCase(2000, 2000, 320, 320)]
    public async Task ALargeImage_FitsWithinTheMaximumEdge_KeepingItsAspectRatio(
        int width, int height, int expectedWidth, int expectedHeight)
    {
        using var source = new Image<Rgba32>(width, height);

        var (_, thumbnail) = await GenerateAsync(await EncodeAsync(source, (image, s) => image.SaveAsPngAsync(s)));

        Assert.That(SizeOf(thumbnail), Is.EqualTo(new Size(expectedWidth, expectedHeight)));
    }

    [Test]
    public async Task ALargeJpeg_FitsWithinTheMaximumEdge()
    {
        using var source = new Image<Rgb24>(2400, 1200);

        var (_, thumbnail) = await GenerateAsync(await EncodeAsync(source, (image, s) => image.SaveAsJpegAsync(s)));

        Assert.That(SizeOf(thumbnail), Is.EqualTo(new Size(320, 160)));
    }

    // A phone stores a portrait photo landscape, with an EXIF tag saying to turn it.
    [Test]
    public async Task AJpegWithAnExifOrientation_IsTurnedUpright_AndItsExifIsDropped()
    {
        using var source = new Image<Rgb24>(400, 200);
        source.Metadata.ExifProfile = new ExifProfile();
        source.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);

        var (generated, thumbnail) = await GenerateAsync(await EncodeAsync(source, (image, s) => image.SaveAsJpegAsync(s)));

        var info = Image.Identify(thumbnail);
        Assert.Multiple(() =>
        {
            Assert.That(generated, Is.True);
            Assert.That(info.Size, Is.EqualTo(new Size(160, 320)));
            Assert.That(info.Metadata.ExifProfile, Is.Null);
        });
    }

    [Test]
    public async Task AnAnimatedGif_IsThumbnailedFromItsFirstFrame()
    {
        using var source = new Image<Rgba32>(40, 20, new Rgba32(255, 0, 0, 255));
        using var secondFrame = new Image<Rgba32>(40, 20, new Rgba32(0, 0, 255, 255));
        source.Frames.AddFrame(secondFrame.Frames.RootFrame);

        var (generated, thumbnail) = await GenerateAsync(await EncodeAsync(source, (image, s) => image.SaveAsGifAsync(s)));

        using var decoded = Image.Load<Rgba32>(thumbnail);
        Assert.Multiple(() =>
        {
            Assert.That(generated, Is.True);
            Assert.That(decoded.Frames, Has.Count.EqualTo(1));
            Assert.That(decoded[20, 10].R, Is.GreaterThan(200));
        });
    }

    [Test]
    public async Task ATiff_GetsNoThumbnail()
    {
        using var source = new Image<Rgba32>(10, 10);

        var (generated, _) = await GenerateAsync(await EncodeAsync(source, (image, s) => image.SaveAsTiffAsync(s)));

        Assert.That(generated, Is.False);
    }

    [TestCase("plain text, not an image")]
    [TestCase("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\"/></svg>")]
    [TestCase("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n")]
    public async Task SomethingThatIsntARasterImage_GetsNoThumbnail(string content)
    {
        var (generated, thumbnail) = await GenerateAsync(Encoding.UTF8.GetBytes(content));

        Assert.Multiple(() =>
        {
            Assert.That(generated, Is.False);
            Assert.That(thumbnail, Is.Empty);
        });
    }

    // A tiny file can claim enormous dimensions, and decoding it would allocate for all of them.
    // Had it been decoded, this would either have made a thumbnail or thrown on the missing data.
    [Test]
    public async Task AnImageWhoseHeaderClaimsMoreThanThePixelLimit_GetsNoThumbnail_WithoutBeingDecoded()
    {
        using var source = new Image<Rgba32>(2, 2);
        var gif = await EncodeAsync(source, (image, s) => image.SaveAsGifAsync(s));
        var claimedWidth = ushort.MaxValue;
        var claimedHeight = (ushort)(ImageSharpThumbnailGenerator.MaxSourcePixels / claimedWidth + 1);
        // The GIF logical screen size follows the six-byte signature, little-endian.
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(6), claimedWidth);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(8), claimedHeight);

        var (generated, thumbnail) = await GenerateAsync(gif);

        Assert.Multiple(() =>
        {
            Assert.That(Image.Identify(gif).Size, Is.EqualTo(new Size(claimedWidth, claimedHeight)));
            Assert.That(generated, Is.False);
            Assert.That(thumbnail, Is.Empty);
        });
    }

    private async Task<(bool Generated, byte[] Thumbnail)> GenerateAsync(byte[] source)
    {
        using var destination = new MemoryStream();
        var generated = await _generator.TryGenerateAsync(new MemoryStream(source), destination);
        return (generated, destination.ToArray());
    }

    private static async Task<byte[]> EncodeAsync<TImage>(TImage image, Func<TImage, Stream, Task> save)
    {
        using var stream = new MemoryStream();
        await save(image, stream);
        return stream.ToArray();
    }

    private static Size SizeOf(byte[] image) => Image.Identify(image).Size;
}
