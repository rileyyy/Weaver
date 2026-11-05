using System.Text;
using Microsoft.Extensions.Options;
using Weaver.Application.Attachments;
using Weaver.Infrastructure.Attachments;

namespace Weaver.Infrastructure.Tests;

[TestFixture]
public class FileSystemAttachmentStoreTests
{
    private string _root = null!;
    private FileSystemAttachmentStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "weaver-attachments-" + Guid.NewGuid().ToString("N"), "nested");
        _store = new FileSystemAttachmentStore(Options.Create(new AttachmentOptions { Path = _root }));
    }

    [TearDown]
    public void TearDown()
    {
        var parent = Path.GetDirectoryName(_root)!;
        if (Directory.Exists(parent))
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Test]
    public void EnsureRootExists_CreatesTheDirectory()
    {
        _store.EnsureRootExists();

        Assert.That(Directory.Exists(_root), Is.True);
    }

    [Test]
    public async Task WriteAsync_StoresTheFileUnderItsIdAlone_AndOpenReadAsyncReadsItBack()
    {
        var id = Guid.NewGuid();
        _store.EnsureRootExists();

        await _store.WriteAsync(id, new MemoryStream(Encoding.UTF8.GetBytes("hello")));
        await using var read = await _store.OpenReadAsync(id);
        using var reader = new StreamReader(read);

        Assert.Multiple(async () =>
        {
            Assert.That(Directory.GetFiles(_root).Select(Path.GetFileName), Is.EqualTo(new[] { id.ToString() }));
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("hello"));
        });
    }

    [Test]
    public void WriteAsync_WhenTheContentFailsPartWay_LeavesNoFile()
    {
        var id = Guid.NewGuid();
        _store.EnsureRootExists();

        Assert.ThrowsAsync<IOException>(() => _store.WriteAsync(id, new FailingAfterSomeBytesStream()));

        Assert.That(Directory.GetFiles(_root), Is.Empty);
    }

    [Test]
    public async Task DeleteAsync_RemovesTheFile_AndAMissingFileIsNotAnError()
    {
        var id = Guid.NewGuid();
        _store.EnsureRootExists();
        await _store.WriteAsync(id, new MemoryStream([1]));

        await _store.DeleteAsync(id);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.GetFiles(_root), Is.Empty);
            Assert.DoesNotThrowAsync(() => _store.DeleteAsync(id));
        });
    }

    [Test]
    public void OpenReadAsync_ForAMissingFile_ThrowsFileNotFound()
    {
        _store.EnsureRootExists();

        Assert.ThrowsAsync<FileNotFoundException>(() => _store.OpenReadAsync(Guid.NewGuid()));
    }
}
