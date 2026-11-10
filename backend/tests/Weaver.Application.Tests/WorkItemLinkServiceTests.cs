using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemLinkServiceTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private WorkItemLinkService _links = null!;
    private WorkItemService _workItems = null!;
    private TestSpace _space = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _links = new WorkItemLinkService(_db, FakeCallerContext.Anyone());
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), new FixedServerCalendar(), FakeCallerContext.Anyone());
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task CreateAsync_LinksTwoWorkItems_SeenFromTheFirst()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");

        var link = await _links.CreateAsync(a.Id, b.Id, WorkItemLinkRelation.Related);

        Assert.Multiple(() =>
        {
            Assert.That(link.Type, Is.EqualTo(WorkItemLinkRelation.Related));
            Assert.That(link.LinkedWorkItemId, Is.EqualTo(b.Id));
            Assert.That(link.LinkedWorkItemNumber, Is.EqualTo(b.Number));
            Assert.That(link.LinkedWorkItemTitle, Is.EqualTo("B"));
            Assert.That(link.LinkedWorkItemSpaceId, Is.EqualTo(_space.Id));
        });
    }

    [Test]
    public async Task CreateAsync_AcrossSpaces_IsAllowed()
    {
        var garden = TestSpaces.Add(_db, "Garden");
        var a = await CreateAsync("A");
        var b = await _workItems.CreateAsync("B", null, null, garden.ToDo, spaceId: garden.Id);

        var link = await _links.CreateAsync(a.Id, b.Id, WorkItemLinkRelation.Blocks);

        Assert.That(link.LinkedWorkItemSpaceId, Is.EqualTo(garden.Id));
    }

    [Test]
    public async Task Blocks_IsReadAsBlockedByFromTheOtherSide()
    {
        var drywall = await CreateAsync("Drywall");
        var paint = await CreateAsync("Paint");

        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        var fromDrywall = (await _links.ListForWorkItemAsync(drywall.Id)).Single();
        var fromPaint = (await _links.ListForWorkItemAsync(paint.Id)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(fromDrywall.Type, Is.EqualTo(WorkItemLinkRelation.Blocks));
            Assert.That(fromDrywall.LinkedWorkItemId, Is.EqualTo(paint.Id));
            Assert.That(fromPaint.Type, Is.EqualTo(WorkItemLinkRelation.BlockedBy));
            Assert.That(fromPaint.LinkedWorkItemId, Is.EqualTo(drywall.Id));
        });
    }

    [Test]
    public async Task CreateAsync_AsBlockedBy_IsStoredOnceAsBlocksFromTheBlocker()
    {
        var drywall = await CreateAsync("Drywall");
        var paint = await CreateAsync("Paint");

        var created = await _links.CreateAsync(paint.Id, drywall.Id, WorkItemLinkRelation.BlockedBy);

        var stored = _db.WorkItemLinks.Single();
        Assert.Multiple(() =>
        {
            Assert.That(created.Type, Is.EqualTo(WorkItemLinkRelation.BlockedBy));
            Assert.That(stored.Type, Is.EqualTo(WorkItemLinkType.Blocks));
            Assert.That(stored.WorkItemId, Is.EqualTo(drywall.Id));
            Assert.That(stored.LinkedWorkItemId, Is.EqualTo(paint.Id));
        });
    }

    [Test]
    public async Task Duplicates_IsReadAsDuplicatedByFromTheOtherSide()
    {
        var copy = await CreateAsync("Copy");
        var original = await CreateAsync("Original");

        await _links.CreateAsync(original.Id, copy.Id, WorkItemLinkRelation.DuplicatedBy);

        Assert.Multiple(async () =>
        {
            Assert.That((await _links.ListForWorkItemAsync(copy.Id)).Single().Type, Is.EqualTo(WorkItemLinkRelation.Duplicates));
            Assert.That((await _links.ListForWorkItemAsync(original.Id)).Single().Type, Is.EqualTo(WorkItemLinkRelation.DuplicatedBy));
        });
    }

    [Test]
    public async Task CreateAsync_WithSameId_ThrowsSelfWorkItemLinkException()
    {
        var a = await CreateAsync("A");

        Assert.ThrowsAsync<SelfWorkItemLinkException>(() => _links.CreateAsync(a.Id, a.Id, WorkItemLinkRelation.Related));
    }

    [Test]
    public void CreateAsync_WithUnknownWorkItem_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _links.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), WorkItemLinkRelation.Related));
    }

    [Test]
    public async Task CreateAsync_WhenAlreadyLinked_ThrowsDuplicateWorkItemLinkException()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        await _links.CreateAsync(a.Id, b.Id, WorkItemLinkRelation.Related);

        Assert.ThrowsAsync<DuplicateWorkItemLinkException>(() => _links.CreateAsync(a.Id, b.Id, WorkItemLinkRelation.Related));
    }

    [Test]
    public async Task CreateAsync_WhenAlreadyLinkedInReverse_ThrowsDuplicateWorkItemLinkException()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        await _links.CreateAsync(a.Id, b.Id, WorkItemLinkRelation.Related);

        Assert.ThrowsAsync<DuplicateWorkItemLinkException>(() => _links.CreateAsync(b.Id, a.Id, WorkItemLinkRelation.Related));
    }

    [Test]
    public async Task CreateAsync_WhenAlreadyLinkedWithAnotherType_ThrowsDuplicateWorkItemLinkException()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        await _links.CreateAsync(a.Id, b.Id, WorkItemLinkRelation.Related);

        Assert.ThrowsAsync<DuplicateWorkItemLinkException>(() => _links.CreateAsync(b.Id, a.Id, WorkItemLinkRelation.Blocks));
    }

    [Test]
    public async Task CreateAsync_BlocksTheSameWayTwice_IsADuplicateNotACycle()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        await _links.CreateAsync(a.Id, b.Id, WorkItemLinkRelation.Blocks);

        Assert.ThrowsAsync<DuplicateWorkItemLinkException>(() => _links.CreateAsync(b.Id, a.Id, WorkItemLinkRelation.BlockedBy));
    }

    [TestCase(WorkItemLinkRelation.Blocks)]
    [TestCase(WorkItemLinkRelation.BlockedBy)]
    public async Task CreateAsync_BlocksBackAgainstAnExistingBlocks_ThrowsBlockingCycleException(WorkItemLinkRelation fromB)
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        await _links.CreateAsync(a.Id, b.Id, WorkItemLinkRelation.Blocks);
        var reverse = fromB == WorkItemLinkRelation.Blocks
            ? (Func<Task>)(() => _links.CreateAsync(b.Id, a.Id, WorkItemLinkRelation.Blocks))
            : () => _links.CreateAsync(a.Id, b.Id, WorkItemLinkRelation.BlockedBy);

        Assert.ThrowsAsync<BlockingCycleException>(async () => await reverse());
    }

    [Test]
    public async Task ListForWorkItemAsync_ReturnsLinksRegardlessOfWhichSideCreatedThem_InCreationOrder()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        var c = await CreateAsync("C");
        await _links.CreateAsync(a.Id, b.Id, WorkItemLinkRelation.Related);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _links.CreateAsync(c.Id, a.Id, WorkItemLinkRelation.Blocks);

        var linksForA = await _links.ListForWorkItemAsync(a.Id);

        Assert.That(linksForA.Select(l => (l.LinkedWorkItemTitle, l.Type)), Is.EqualTo(new[]
        {
            ("B", WorkItemLinkRelation.Related),
            ("C", WorkItemLinkRelation.BlockedBy),
        }));
    }

    [Test]
    public async Task DeleteAsync_RemovesTheLink()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        var link = await _links.CreateAsync(a.Id, b.Id, WorkItemLinkRelation.Blocks);

        await _links.DeleteAsync(link.Id);

        Assert.That(await _links.ListForWorkItemAsync(a.Id), Is.Empty);
    }

    [Test]
    public void DeleteAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _links.DeleteAsync(Guid.NewGuid()));
    }

    private Task<WorkItem> CreateAsync(string title) =>
        _workItems.CreateAsync(title, null, null, _space.ToDo, spaceId: _space.Id);
}
