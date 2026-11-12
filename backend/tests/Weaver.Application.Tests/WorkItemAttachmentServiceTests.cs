using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weaver.Application.Attachments;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemAttachmentServiceTests
{
    private const int MaxMegabytes = 1;
    private const int MaxBytes = MaxMegabytes * 1024 * 1024;

    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private InMemoryAttachmentStore _store = null!;
    private FakeThumbnailGenerator _thumbnails = null!;
    private FakeCallerContext _caller = null!;
    private WorkItemAttachmentService _attachments = null!;
    private WorkItemService _workItems = null!;
    private TestSpace _space = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _store = new InMemoryAttachmentStore();
        _thumbnails = new FakeThumbnailGenerator();
        _caller = FakeCallerContext.Anyone();
        _attachments = ServiceOver(_db);
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), new FixedServerCalendar(), _caller);
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task AddAsync_StoresTheFileUnderTheAttachmentsId_AndReturnsItsMetadata()
    {
        var item = await CreateAsync("Kitchen");

        var added = await _attachments.AddAsync(item.Id, "../quote.pdf", "application/pdf", Content("PDF bytes"));

        Assert.Multiple(() =>
        {
            Assert.That(added.WorkItemId, Is.EqualTo(item.Id));
            Assert.That(added.FileName, Is.EqualTo("quote.pdf"));
            Assert.That(added.ContentType, Is.EqualTo("application/pdf"));
            Assert.That(added.SizeBytes, Is.EqualTo(9));
            Assert.That(added.CreatedAtUtc, Is.EqualTo(_clock.GetUtcNow()));
            Assert.That(Encoding.UTF8.GetString(_store.Files[added.Id]), Is.EqualTo("PDF bytes"));
        });
    }

    [Test]
    public async Task AddAsync_RecordsWhoUploadedIt()
    {
        var item = await CreateAsync("Kitchen");

        var added = await _attachments.AddAsync(item.Id, "a.txt", "text/plain", Content("a"));

        var row = await _db.WorkItemAttachments.SingleAsync(a => a.Id == added.Id);
        Assert.That(row.UploadedByUserId, Is.EqualTo(_caller.Current.AccountId));
    }

    [Test]
    public void AddAsync_ForAnUnknownItem_ThrowsEntityNotFoundException_AndStoresNothing()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _attachments.AddAsync(Guid.NewGuid(), "a.txt", "text/plain", Content("a")));

        Assert.That(_store.Files, Is.Empty);
    }

    [Test]
    public async Task AddAsync_ForATrashedItem_ThrowsEntityNotFoundException()
    {
        var item = await CreateAsync("Kitchen");
        await TrashService().MoveToTrashAsync(item.Id);

        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _attachments.AddAsync(item.Id, "a.txt", "text/plain", Content("a")));
    }

    [Test]
    public async Task AddAsync_AtTheSizeLimit_IsAccepted()
    {
        var item = await CreateAsync("Kitchen");

        var added = await _attachments.AddAsync(item.Id, "big.bin", null, new MemoryStream(new byte[MaxBytes]));

        Assert.That(added.SizeBytes, Is.EqualTo(MaxBytes));
    }

    [Test]
    public async Task AddAsync_OverTheSizeLimit_ThrowsAttachmentTooLarge_AndKeepsNothing()
    {
        var item = await CreateAsync("Kitchen");

        var thrown = Assert.ThrowsAsync<AttachmentTooLargeException>(() =>
            _attachments.AddAsync(item.Id, "big.bin", null, new MemoryStream(new byte[MaxBytes + 1])));

        Assert.Multiple(async () =>
        {
            Assert.That(thrown!.Message, Does.Contain("1 MB"));
            Assert.That(_store.Files, Is.Empty);
            Assert.That(await _db.WorkItemAttachments.CountAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task AddAsync_OverTheSizeLimit_StopsReadingSoonAfterTheLimit()
    {
        var item = await CreateAsync("Kitchen");
        var body = new CountingStream(new MemoryStream(new byte[MaxBytes * 4]));

        Assert.ThrowsAsync<AttachmentTooLargeException>(() => _attachments.AddAsync(item.Id, "big.bin", null, body));

        Assert.That(body.BytesRead, Is.LessThan(MaxBytes * 2));
    }

    [Test]
    public async Task AddAsync_WithAnEmptyFile_ThrowsDomainValidationException_AndKeepsNothing()
    {
        var item = await CreateAsync("Kitchen");

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _attachments.AddAsync(item.Id, "empty.txt", "text/plain", new MemoryStream()));

        Assert.That(_store.Files, Is.Empty);
    }

    [Test]
    public async Task AddAsync_WhenTheFileCantBeWritten_SavesNoRow()
    {
        var item = await CreateAsync("Kitchen");
        _store.FailWrites = true;

        Assert.ThrowsAsync<IOException>(() => _attachments.AddAsync(item.Id, "a.txt", "text/plain", Content("a")));

        Assert.That(await _db.WorkItemAttachments.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task AddAsync_WhenTheRowCantBeSaved_RemovesTheFile()
    {
        var saves = new SwitchableSaveFailure();
        using var db = TestDatabase.Create(_clock, saves);
        var space = TestSpaces.Add(db);
        var item = await new WorkItemService(db, new IterativeWorkItemHierarchy(db), new FixedServerCalendar(), _caller)
            .CreateAsync("Kitchen", null, null, space.ToDo, spaceId: space.Id);
        saves.Fail = true;

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            ServiceOver(db).AddAsync(item.Id, "a.txt", "text/plain", Content("a")));

        Assert.That(_store.Files, Is.Empty);
    }

    [Test]
    public async Task AddAsync_OfAnImage_StoresItsThumbnailUnderTheSameId_FromTheStoredFile()
    {
        var item = await CreateAsync("Kitchen");
        _thumbnails.Produces = [7, 7, 7];

        var added = await _attachments.AddAsync(item.Id, "photo.jpg", "image/jpeg", Content("JPEG bytes"));

        var row = await _db.WorkItemAttachments.SingleAsync(a => a.Id == added.Id);
        Assert.Multiple(() =>
        {
            Assert.That(added.HasThumbnail, Is.True);
            Assert.That(row.HasThumbnail, Is.True);
            Assert.That(_store.Thumbnails[added.Id], Is.EqualTo(new byte[] { 7, 7, 7 }));
            Assert.That(Encoding.UTF8.GetString(_thumbnails.LastSource!), Is.EqualTo("JPEG bytes"));
        });
    }

    [Test]
    public async Task AddAsync_OfAFileTheGeneratorDeclines_HasNoThumbnail()
    {
        var item = await CreateAsync("Kitchen");

        var added = await _attachments.AddAsync(item.Id, "a.txt", "text/plain", Content("a"));

        Assert.Multiple(() =>
        {
            Assert.That(added.HasThumbnail, Is.False);
            Assert.That(_store.Files, Contains.Key(added.Id));
            Assert.That(_store.Thumbnails, Is.Empty);
        });
    }

    [Test]
    public async Task AddAsync_WhenMakingTheThumbnailThrows_StillAddsTheAttachmentWithoutOne()
    {
        var item = await CreateAsync("Kitchen");
        _thumbnails.Throws = new InvalidDataException("Corrupt image.");

        var added = await _attachments.AddAsync(item.Id, "broken.png", "image/png", Content("not really a png"));

        Assert.Multiple(async () =>
        {
            Assert.That(added.HasThumbnail, Is.False);
            Assert.That(await _attachments.ListForWorkItemAsync(item.Id), Has.Count.EqualTo(1));
            Assert.That(_store.Files, Contains.Key(added.Id));
            Assert.That(_store.Thumbnails, Is.Empty);
        });
    }

    [Test]
    public async Task AddAsync_WhenTheThumbnailCantBeWritten_StillAddsTheAttachmentWithoutOne()
    {
        var item = await CreateAsync("Kitchen");
        _thumbnails.Produces = [7];
        _store.FailThumbnailWrites = true;

        var added = await _attachments.AddAsync(item.Id, "photo.jpg", "image/jpeg", Content("JPEG bytes"));

        Assert.Multiple(() =>
        {
            Assert.That(added.HasThumbnail, Is.False);
            Assert.That(_store.Files, Contains.Key(added.Id));
        });
    }

    [Test]
    public async Task AddAsync_WhenTheRowCantBeSaved_RemovesTheFileAndItsThumbnail()
    {
        var saves = new SwitchableSaveFailure();
        using var db = TestDatabase.Create(_clock, saves);
        var space = TestSpaces.Add(db);
        var item = await new WorkItemService(db, new IterativeWorkItemHierarchy(db), new FixedServerCalendar(), _caller)
            .CreateAsync("Kitchen", null, null, space.ToDo, spaceId: space.Id);
        _thumbnails.Produces = [7];
        saves.Fail = true;

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            ServiceOver(db).AddAsync(item.Id, "photo.jpg", "image/jpeg", Content("JPEG bytes")));

        Assert.Multiple(() =>
        {
            Assert.That(_store.Files, Is.Empty);
            Assert.That(_store.Thumbnails, Is.Empty);
        });
    }

    [Test]
    public async Task AddAsync_WhenCancelledWhileMakingTheThumbnail_KeepsNothing()
    {
        var item = await CreateAsync("Kitchen");
        _thumbnails.Throws = new OperationCanceledException();

        Assert.CatchAsync<OperationCanceledException>(() =>
            _attachments.AddAsync(item.Id, "photo.jpg", "image/jpeg", Content("JPEG bytes")));

        Assert.Multiple(async () =>
        {
            Assert.That(_store.Files, Is.Empty);
            Assert.That(await _db.WorkItemAttachments.CountAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task ListForWorkItemAsync_SaysWhichAttachmentsHaveAThumbnail()
    {
        var item = await CreateAsync("Kitchen");
        await _attachments.AddAsync(item.Id, "a.txt", "text/plain", Content("a"));
        _thumbnails.Produces = [7];
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _attachments.AddAsync(item.Id, "photo.jpg", "image/jpeg", Content("JPEG bytes"));

        var listed = await _attachments.ListForWorkItemAsync(item.Id);

        Assert.That(listed.Select(a => a.HasThumbnail), Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public async Task OpenThumbnailAsync_ReturnsTheThumbnailsBytes()
    {
        var item = await CreateAsync("Kitchen");
        _thumbnails.Produces = [7, 8, 9];
        var added = await _attachments.AddAsync(item.Id, "photo.jpg", "image/jpeg", Content("JPEG bytes"));

        await using var thumbnail = await _attachments.OpenThumbnailAsync(added.Id);
        using var read = new MemoryStream();
        await thumbnail.CopyToAsync(read);

        Assert.That(read.ToArray(), Is.EqualTo(new byte[] { 7, 8, 9 }));
    }

    [Test]
    public async Task OpenThumbnailAsync_ForAnAttachmentWithoutOne_ThrowsANotFoundDomainException()
    {
        var item = await CreateAsync("Kitchen");
        var added = await _attachments.AddAsync(item.Id, "a.txt", "text/plain", Content("a"));

        var thrown = Assert.ThrowsAsync<AttachmentThumbnailNotFoundException>(() => _attachments.OpenThumbnailAsync(added.Id));

        Assert.That(thrown!.Kind, Is.EqualTo(DomainErrorKind.NotFound));
    }

    [Test]
    public void OpenThumbnailAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _attachments.OpenThumbnailAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task DeleteAsync_RemovesTheThumbnailToo()
    {
        var item = await CreateAsync("Kitchen");
        _thumbnails.Produces = [7];
        var added = await _attachments.AddAsync(item.Id, "photo.jpg", "image/jpeg", Content("JPEG bytes"));

        await _attachments.DeleteAsync(added.Id);

        Assert.That(_store.Thumbnails, Is.Empty);
    }

    [Test]
    public async Task ListForWorkItemAsync_ReturnsOnlyThatItemsAttachments_OldestFirst()
    {
        var item = await CreateAsync("Kitchen");
        var other = await CreateAsync("Garden");
        await _attachments.AddAsync(item.Id, "first.txt", "text/plain", Content("1"));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _attachments.AddAsync(other.Id, "other.txt", "text/plain", Content("o"));
        await _attachments.AddAsync(item.Id, "second.txt", "text/plain", Content("2"));

        var listed = await _attachments.ListForWorkItemAsync(item.Id);

        Assert.That(listed.Select(a => a.FileName), Is.EqualTo(new[] { "first.txt", "second.txt" }));
    }

    [Test]
    public async Task OpenContentAsync_ReturnsTheMetadataAndTheBytes()
    {
        var item = await CreateAsync("Kitchen");
        var added = await _attachments.AddAsync(item.Id, "a.txt", "text/plain", Content("hello"));

        var opened = await _attachments.OpenContentAsync(added.Id);
        await using var content = opened.Content;
        using var reader = new StreamReader(content);

        Assert.Multiple(async () =>
        {
            Assert.That(opened.Attachment, Is.EqualTo(added));
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("hello"));
        });
    }

    [Test]
    public void OpenContentAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _attachments.OpenContentAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task DeleteAsync_RemovesTheRowAndTheFile()
    {
        var item = await CreateAsync("Kitchen");
        var added = await _attachments.AddAsync(item.Id, "a.txt", "text/plain", Content("a"));

        await _attachments.DeleteAsync(added.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(await _attachments.ListForWorkItemAsync(item.Id), Is.Empty);
            Assert.That(_store.Files, Is.Empty);
        });
    }

    [Test]
    public void DeleteAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _attachments.DeleteAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task ATrashedItemsAttachments_AreKeptButHidden_UntilItIsRestored()
    {
        var trash = TrashService();
        var item = await CreateAsync("Kitchen");
        var added = await _attachments.AddAsync(item.Id, "a.txt", "text/plain", Content("a"));

        await trash.MoveToTrashAsync(item.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(await _attachments.ListForWorkItemAsync(item.Id), Is.Empty);
            Assert.That(await _db.WorkItemAttachments.IgnoreQueryFilters().CountAsync(), Is.EqualTo(1));
            Assert.That(_store.Files, Contains.Key(added.Id));
            Assert.ThrowsAsync<EntityNotFoundException>(() => _attachments.OpenContentAsync(added.Id));
            Assert.ThrowsAsync<EntityNotFoundException>(() => _attachments.DeleteAsync(added.Id));
        });

        await trash.RestoreAsync(item.Id);

        Assert.That(await _attachments.ListForWorkItemAsync(item.Id), Has.Count.EqualTo(1));
    }

    private WorkItemAttachmentService ServiceOver(WeaverDbContext db) => new(
        db,
        _caller,
        _store,
        _thumbnails,
        Options.Create(new AttachmentOptions { MaxFileSizeMegabytes = MaxMegabytes }),
        NullLogger<WorkItemAttachmentService>.Instance);

    private WorkItemTrashService TrashService() =>
        new(_db, new IterativeWorkItemHierarchy(_db), _caller, _clock);

    private Task<WorkItem> CreateAsync(string title) =>
        _workItems.CreateAsync(title, null, null, _space.ToDo, spaceId: _space.Id);

    private static MemoryStream Content(string text) => new(Encoding.UTF8.GetBytes(text));
}
