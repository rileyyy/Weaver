using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// The brief listings agents read (children, every item) and the lookup by Number. Each listing
/// must keep the rules of its full-item twin: order, Trash and Space.
/// </summary>
[TestFixture]
public class WorkItemSummaryListingTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private WorkItemService _service = null!;
    private WorkItemTrashService _trash = null!;
    private TestSpace _home = null!;
    private TestSpace _software = null!;
    private int _lastNumber;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        var hierarchy = new IterativeWorkItemHierarchy(_db);
        _service = new WorkItemService(_db, hierarchy, FakeCallerContext.Anyone());
        _trash = new WorkItemTrashService(_db, hierarchy, FakeCallerContext.Anyone(), _clock);
        _home = TestSpaces.Add(_db, "Home");
        _software = TestSpaces.Add(_db, "Software");
        _lastNumber = 0;
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetChildSummariesAsync_ListsDirectChildrenInRankOrder_WithNamesAndTheirParent()
    {
        var parent = await CreateAsync("Parent");
        var first = await CreateAsync("First", parent.Id);
        var second = await CreateAsync("Second", parent.Id, afterId: first.Id);
        await CreateAsync("Grandchild", second.Id);

        var children = await _service.GetChildSummariesAsync(parent.Id);

        Assert.Multiple(() =>
        {
            Assert.That(children.Select(c => c.Id), Is.EqualTo(new[] { first.Id, second.Id }));
            Assert.That(children[1], Is.EqualTo(new WorkItemSummary(
                second.Id, second.Number, "Second", _home.Id, "Home", parent.Id, "To Do", null, null, null, null, ChildCount: 1)));
        });
    }

    [Test]
    public async Task GetChildSummariesAsync_WithOnlyASpace_ListsItsTopLevelItems()
    {
        var top = await CreateAsync("Top");
        await CreateAsync("Child", top.Id);
        await CreateAsync("Elsewhere", space: _software);

        var tops = await _service.GetChildSummariesAsync(null, _home.Id);

        Assert.Multiple(() =>
        {
            Assert.That(tops.Select(c => c.Id), Is.EqualTo(new[] { top.Id }));
            Assert.That(tops.Single().ParentId, Is.Null);
        });
    }

    [Test]
    public async Task GetChildSummariesAsync_LeavesTheTrashOut()
    {
        var parent = await CreateAsync("Parent");
        var kept = await CreateAsync("Kept", parent.Id);
        var trashed = await CreateAsync("Trashed", parent.Id);
        await _trash.MoveToTrashAsync(trashed.Id);

        var children = await _service.GetChildSummariesAsync(parent.Id);

        Assert.That(children.Select(c => c.Id), Is.EqualTo(new[] { kept.Id }));
    }

    [Test]
    public void GetChildSummariesAsync_WithNeitherParentNorSpace_IsRejected()
    {
        Assert.ThrowsAsync<DomainValidationException>(() => _service.GetChildSummariesAsync(null));
    }

    [Test]
    public async Task GetAncestorSummariesAsync_ListsEveryAncestor_FromTheRootDown()
    {
        var root = await CreateAsync("House");
        var middle = await CreateAsync("Kitchen", root.Id);
        var parent = await CreateAsync("Cabinets", middle.Id);
        var item = await CreateAsync("Hinges", parent.Id);

        var ancestors = await _service.GetAncestorSummariesAsync(item.Id);

        Assert.Multiple(() =>
        {
            Assert.That(ancestors.Select(a => a.Title), Is.EqualTo(new[] { "House", "Kitchen", "Cabinets" }));
            Assert.That(ancestors[0], Is.EqualTo(new WorkItemSummary(
                root.Id, root.Number, "House", _home.Id, "Home", null, "To Do", null, null, null, null, ChildCount: 1)));
        });
    }

    [Test]
    public async Task GetAncestorSummariesAsync_ForATopLevelItem_IsEmpty()
    {
        var top = await CreateAsync("Top");

        Assert.That(await _service.GetAncestorSummariesAsync(top.Id), Is.Empty);
    }

    [Test]
    public async Task GetAncestorSummariesAsync_LeavesTheTrashOut()
    {
        var root = await CreateAsync("Root");
        var parent = await CreateAsync("Parent", root.Id);
        var item = await CreateAsync("Item", parent.Id);
        await TrashOnlyAsync(parent.Id);

        var ancestors = await _service.GetAncestorSummariesAsync(item.Id);

        Assert.That(ancestors.Select(a => a.Id), Is.EqualTo(new[] { root.Id }));
    }

    [Test]
    public async Task GetAncestorSummariesAsync_OnACorruptTreeWithACycle_StopsInsteadOfLooping()
    {
        var a = await CreateAsync("A");
        var b = await CreateAsync("B", a.Id);
        var item = await CreateAsync("Item", b.Id);
        var trackedA = (await _db.WorkItems.FindAsync(a.Id))!;
        _db.Entry(trackedA).Property(w => w.ParentId).CurrentValue = b.Id;
        await _db.SaveChangesAsync();

        var ancestors = await _service.GetAncestorSummariesAsync(item.Id);

        Assert.That(ancestors.Select(s => s.Id), Is.EquivalentTo(new[] { a.Id, b.Id }));
    }

    [Test]
    public async Task GetAllSummariesAsync_PagesInRankThenNumberOrder_WithEachItemsParent()
    {
        var first = await CreateAsync("First");
        var second = await CreateAsync("Second", afterId: first.Id);
        var child = await CreateAsync("Child", first.Id);

        var page1 = await _service.GetAllSummariesAsync(offset: 0, limit: 2);
        var page2 = await _service.GetAllSummariesAsync(offset: 2, limit: 2);

        var full = await _service.GetAllAsync(offset: 0, limit: 3);
        Assert.Multiple(() =>
        {
            Assert.That(page1.Concat(page2).Select(s => s.Id), Is.EqualTo(full.Select(w => w.Id)));
            Assert.That(page1.Concat(page2).Single(s => s.Id == child.Id).ParentId, Is.EqualTo(first.Id));
            Assert.That(page1.Concat(page2).Single(s => s.Id == second.Id).ParentId, Is.Null);
        });
    }

    [Test]
    public async Task GetAllSummariesAsync_InASpace_ListsOnlyThatSpace_AndLeavesTheTrashOut()
    {
        var home = await CreateAsync("Home item");
        var trashed = await CreateAsync("Trashed");
        await CreateAsync("Software item", space: _software);
        await _trash.MoveToTrashAsync(trashed.Id);

        var items = await _service.GetAllSummariesAsync(offset: 0, limit: 10, _home.Id);

        Assert.That(items.Select(s => s.Id), Is.EqualTo(new[] { home.Id }));
    }

    [TestCase(-1, 10)]
    [TestCase(0, 0)]
    [TestCase(0, WorkItemService.MaxPageSize + 1)]
    public void GetAllSummariesAsync_WithAnInvalidPage_IsRejected(int offset, int limit)
    {
        Assert.ThrowsAsync<DomainValidationException>(() => _service.GetAllSummariesAsync(offset, limit));
    }

    [Test]
    public async Task GetByNumberAsync_FindsThatItem()
    {
        await CreateAsync("One");
        var two = await CreateAsync("Two");

        var found = await _service.GetByNumberAsync(two.Number);

        Assert.That(found?.Id, Is.EqualTo(two.Id));
    }

    [Test]
    public async Task GetByNumberAsync_ForAnUnknownOrTrashedNumber_ReturnsNull()
    {
        var trashed = await CreateAsync("Trashed");
        await _trash.MoveToTrashAsync(trashed.Id);

        var trashedLookup = await _service.GetByNumberAsync(trashed.Number);
        var unknownLookup = await _service.GetByNumberAsync(999);

        Assert.Multiple(() =>
        {
            Assert.That(trashedLookup, Is.Null);
            Assert.That(unknownLookup, Is.Null);
        });
    }

    private async Task<WorkItem> CreateAsync(string title, Guid? parentId = null, Guid? afterId = null, TestSpace? space = null)
    {
        var item = await _service.CreateAsync(
            title,
            null,
            parentId,
            (space ?? _home).ToDo,
            afterId,
            spaceId: parentId is null ? (space ?? _home).Id : null);
        NumberLikePostgres(item);
        return item;
    }

    /// <summary>
    /// Trashes one item without its subtree, which a delete never does, so the ancestor walk
    /// is shown to rely on the Trash filter rather than on the subtree going with it.
    /// </summary>
    private async Task TrashOnlyAsync(Guid id)
    {
        var tracked = await _db.WorkItems.FindAsync(id);
        tracked!.MoveToTrash(id, _clock.GetUtcNow());
        await _db.SaveChangesAsync();
    }

    /// <summary>Postgres numbers items from an identity column, which InMemory doesn't generate.</summary>
    private void NumberLikePostgres(WorkItem item)
    {
        var tracked = _db.WorkItems.Find(item.Id)!;
        _db.Entry(tracked).Property(w => w.Number).CurrentValue = ++_lastNumber;
        _db.SaveChanges();
    }
}
