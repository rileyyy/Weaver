using Microsoft.Extensions.Options;
using Weaver.Application.Attachments;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class AttachmentFileCleanupTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private InMemoryAttachmentStore _store = null!;
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
        _caller = FakeCallerContext.Anyone();
        _attachments = new WorkItemAttachmentService(_db, _caller, _store, Options.Create(new AttachmentOptions()));
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), new FixedServerCalendar(), _caller);
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task PurgeAsync_DeletesTheFilesOfTrashedItems_AfterTheirRowsAreGone_AndLeavesOtherItemsFiles()
    {
        var purged = await CreateAsync("Purged");
        var kept = await CreateAsync("Kept");
        var purgedFile = await AttachAsync(purged.Id);
        var secondPurgedFile = await AttachAsync(purged.Id);
        var keptFile = await AttachAsync(kept.Id);
        await TrashAsync(purged.Id);
        var filesWhenRowsWerePurged = new List<Guid>();

        await new AttachmentFileCleanup(_db, _store).PurgeAsync([purged.Id], _ =>
        {
            filesWhenRowsWerePurged.AddRange(_store.Files.Keys);
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(filesWhenRowsWerePurged, Is.EquivalentTo(new[] { purgedFile, secondPurgedFile, keptFile }));
            Assert.That(_store.Files.Keys, Is.EquivalentTo(new[] { keptFile }));
        });
    }

    [Test]
    public async Task PurgeAsync_KeepsTheFiles_WhenPurgingTheRowsFails()
    {
        var purged = await CreateAsync("Purged");
        var file = await AttachAsync(purged.Id);
        await TrashAsync(purged.Id);

        Assert.ThrowsAsync<InvalidOperationException>(() => new AttachmentFileCleanup(_db, _store)
            .PurgeAsync([purged.Id], _ => throw new InvalidOperationException("purge failed")));

        Assert.That(_store.Files.Keys, Does.Contain(file));
    }

    private Task TrashAsync(Guid id) =>
        new WorkItemTrashService(_db, new IterativeWorkItemHierarchy(_db), _caller, _clock).MoveToTrashAsync(id);

    private async Task<Guid> AttachAsync(Guid workItemId) =>
        (await _attachments.AddAsync(workItemId, "a.txt", "text/plain", new MemoryStream([1]))).Id;

    private Task<WorkItem> CreateAsync(string title) =>
        _workItems.CreateAsync(title, null, null, _space.ToDo, spaceId: _space.Id);
}
