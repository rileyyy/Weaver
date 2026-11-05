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
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), _caller);
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task DeleteFilesForWorkItemsAsync_DeletesTheFilesOfTrashedItems_AndLeavesOtherItemsFiles()
    {
        var purged = await CreateAsync("Purged");
        var kept = await CreateAsync("Kept");
        var purgedFile = await AttachAsync(purged.Id);
        var secondPurgedFile = await AttachAsync(purged.Id);
        var keptFile = await AttachAsync(kept.Id);
        await new WorkItemTrashService(_db, new IterativeWorkItemHierarchy(_db), _caller, _clock).MoveToTrashAsync(purged.Id);

        await new AttachmentFileCleanup(_db, _store).DeleteFilesForWorkItemsAsync([purged.Id]);

        Assert.That(_store.Files.Keys, Is.EquivalentTo(new[] { keptFile }));
        Assert.That(_store.Files.Keys, Does.Not.Contain(purgedFile).And.Not.Contain(secondPurgedFile));
    }

    private async Task<Guid> AttachAsync(Guid workItemId) =>
        (await _attachments.AddAsync(workItemId, "a.txt", "text/plain", new MemoryStream([1]))).Id;

    private Task<WorkItem> CreateAsync(string title) =>
        _workItems.CreateAsync(title, null, null, _space.ToDo, spaceId: _space.Id);
}
