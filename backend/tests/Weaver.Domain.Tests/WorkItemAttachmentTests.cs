using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class WorkItemAttachmentTests
{
    private readonly Guid _item = Guid.NewGuid();
    private readonly Guid _uploader = Guid.NewGuid();

    [Test]
    public void Create_KeepsWhatItWasGiven()
    {
        var id = Guid.NewGuid();

        var attachment = WorkItemAttachment.Create(id, _item, "report.pdf", "application/pdf", 1234, _uploader);

        Assert.Multiple(() =>
        {
            Assert.That(attachment.Id, Is.EqualTo(id));
            Assert.That(attachment.WorkItemId, Is.EqualTo(_item));
            Assert.That(attachment.FileName, Is.EqualTo("report.pdf"));
            Assert.That(attachment.ContentType, Is.EqualTo("application/pdf"));
            Assert.That(attachment.SizeBytes, Is.EqualTo(1234));
            Assert.That(attachment.UploadedByUserId, Is.EqualTo(_uploader));
            Assert.That(attachment.HasThumbnail, Is.False);
        });
    }

    [Test]
    public void Create_RecordsThatAThumbnailWasStored()
    {
        var attachment = WorkItemAttachment.Create(Guid.NewGuid(), _item, "photo.jpg", "image/jpeg", 1, null, hasThumbnail: true);

        Assert.That(attachment.HasThumbnail, Is.True);
    }

    [TestCase("../../etc/passwd", "passwd")]
    [TestCase(@"..\..\Windows\win.ini", "win.ini")]
    [TestCase("C:\\Users\\me\\Desktop\\quote.pdf", "quote.pdf")]
    [TestCase("/abs/path/photo.jpg", "photo.jpg")]
    [TestCase("  spaced name.txt  ", "spaced name.txt")]
    [TestCase("bad\u0000na\u001fme\u007f.txt", "badname.txt")]
    [TestCase("line\r\nbreak.txt", "linebreak.txt")]
    public void Create_KeepsOnlyTheFilesOwnNameWithoutControlCharacters(string given, string expected)
    {
        var attachment = WorkItemAttachment.Create(Guid.NewGuid(), _item, given, "text/plain", 1, null);

        Assert.That(attachment.FileName, Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("folder/")]
    [TestCase("\u0001\u0002")]
    public void Create_WithNoUsableName_IsCalledAttachment(string? given)
    {
        var attachment = WorkItemAttachment.Create(Guid.NewGuid(), _item, given, "text/plain", 1, null);

        Assert.That(attachment.FileName, Is.EqualTo(WorkItemAttachment.FallbackFileName));
    }

    [Test]
    public void Create_WithALongName_CutsItToTheMaximum_KeepingTheExtension()
    {
        var given = new string('a', 300) + ".pdf";

        var attachment = WorkItemAttachment.Create(Guid.NewGuid(), _item, given, "application/pdf", 1, null);

        Assert.Multiple(() =>
        {
            Assert.That(attachment.FileName, Has.Length.EqualTo(WorkItemAttachment.FileNameMaxLength));
            Assert.That(attachment.FileName, Does.EndWith("a.pdf"));
        });
    }

    [Test]
    public void Create_WithALongName_NeverSplitsASurrogatePair()
    {
        // 254 ASCII characters then emoji, so a plain cut at 255 would land mid-pair.
        var given = new string('a', 254) + string.Concat(Enumerable.Repeat("\U0001F600", 10));

        var attachment = WorkItemAttachment.Create(Guid.NewGuid(), _item, given, "text/plain", 1, null);

        Assert.Multiple(() =>
        {
            Assert.That(attachment.FileName.Length, Is.LessThanOrEqualTo(WorkItemAttachment.FileNameMaxLength));
            Assert.That(char.IsHighSurrogate(attachment.FileName[^1]), Is.False);
        });
    }

    [TestCase(" text/plain ", "text/plain")]
    [TestCase("text/plain; charset=utf-8", "text/plain; charset=utf-8")]
    [TestCase(null, "application/octet-stream")]
    [TestCase("", "application/octet-stream")]
    [TestCase("not a media type", "application/octet-stream")]
    [TestCase("text/plain\r\nX-Injected: 1", "application/octet-stream")]
    public void Create_KeepsAValidContentType_AndOtherwiseFallsBackToBinary(string? given, string expected)
    {
        var attachment = WorkItemAttachment.Create(Guid.NewGuid(), _item, "a.txt", given, 1, null);

        Assert.That(attachment.ContentType, Is.EqualTo(expected));
    }

    [Test]
    public void Create_WithAContentTypeOverTheMaximumLength_FallsBackToBinary()
    {
        var given = "application/" + new string('x', WorkItemAttachment.ContentTypeMaxLength);

        var attachment = WorkItemAttachment.Create(Guid.NewGuid(), _item, "a.bin", given, 1, null);

        Assert.That(attachment.ContentType, Is.EqualTo("application/octet-stream"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Create_WithAnEmptyFile_IsRejected(long size)
    {
        Assert.Throws<DomainValidationException>(() =>
            WorkItemAttachment.Create(Guid.NewGuid(), _item, "a.txt", "text/plain", size, null));
    }
}
