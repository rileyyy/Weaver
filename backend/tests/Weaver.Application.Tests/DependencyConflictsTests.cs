using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class DependencyConflictsTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private WorkItemService _workItems = null!;
    private WorkItemLinkService _links = null!;
    private WorkItemTrashService _trash = null!;
    private DependencyConflicts _conflicts = null!;
    private TestSpace _space = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        var caller = FakeCallerContext.Anyone();
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), new FixedServerCalendar(), caller);
        _links = new WorkItemLinkService(_db, caller);
        _trash = new WorkItemTrashService(_db, new IterativeWorkItemHierarchy(_db), caller, _clock);
        _conflicts = new DependencyConflicts(_db);
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task ListAsync_ABlockedItemStartingBeforeItsBlockerEnds_IsAConflict()
    {
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(6));
        var paint = await CreateAsync("Paint", Nov(5), Nov(8));
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        var conflict = (await _conflicts.ListAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(conflict.Blocked, Is.EqualTo(new DependencyConflictItem(paint.Id, paint.Number, "Paint", _space.Id, "Home", Nov(5), Nov(8))));
            Assert.That(conflict.Blocker, Is.EqualTo(new DependencyConflictItem(drywall.Id, drywall.Number, "Drywall", _space.Id, "Home", Nov(2), Nov(6))));
        });
    }

    [Test]
    public async Task ListAsync_ABlockedItemStartingOnItsBlockersEndDate_IsNotAConflict()
    {
        // The end date is inclusive on the Roadmap, but a blocker can finish and the next item
        // begin on the same day, so only a start before the end date conflicts.
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(6));
        var paint = await CreateAsync("Paint", Nov(6), Nov(8));
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        Assert.That(await _conflicts.ListAsync(), Is.Empty);
    }

    [Test]
    public async Task ListAsync_ABlockedItemStartingAfterItsBlockerEnds_IsNotAConflict()
    {
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(6));
        var paint = await CreateAsync("Paint", Nov(7), Nov(8));
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        Assert.That(await _conflicts.ListAsync(), Is.Empty);
    }

    [Test]
    public async Task ListAsync_ABlockedItemWithNoStartDate_IsNotAConflict()
    {
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(6));
        var paint = await CreateAsync("Paint", null, Nov(3));
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        Assert.That(await _conflicts.ListAsync(), Is.Empty);
    }

    [Test]
    public async Task ListAsync_ABlockerWithNoEndDate_IsNotAConflict()
    {
        var drywall = await CreateAsync("Drywall", Nov(2), null);
        var paint = await CreateAsync("Paint", Nov(1), Nov(8));
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        Assert.That(await _conflicts.ListAsync(), Is.Empty);
    }

    [Test]
    public async Task ListAsync_ARelatedOrDuplicateLink_IsNeverAConflict()
    {
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(6));
        var paint = await CreateAsync("Paint", Nov(3), Nov(8));
        var trim = await CreateAsync("Trim", Nov(3), Nov(8));
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Related);
        await _links.CreateAsync(drywall.Id, trim.Id, WorkItemLinkRelation.Duplicates);

        Assert.That(await _conflicts.ListAsync(), Is.Empty);
    }

    [Test]
    public async Task ListAsync_ADoneBlocker_IsNotAConflict()
    {
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(6));
        var paint = await CreateAsync("Paint", Nov(3), Nov(8));
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        await _workItems.ChangeStatusAsync(drywall.Id, _space.Done);

        Assert.That(await _conflicts.ListAsync(), Is.Empty);
    }

    [Test]
    public async Task ListAsync_ADoneBlockedItem_IsNotAConflict()
    {
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(6));
        var paint = await CreateAsync("Paint", Nov(3), Nov(8));
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        await _workItems.ChangeStatusAsync(paint.Id, _space.Done);

        Assert.That(await _conflicts.ListAsync(), Is.Empty);
    }

    [Test]
    public async Task ListAsync_IgnoresABlockerInTheTrash_UntilItIsRestored()
    {
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(6));
        var paint = await CreateAsync("Paint", Nov(3), Nov(8));
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        await _trash.MoveToTrashAsync(drywall.Id);
        var whileTrashed = await _conflicts.ListAsync();
        await _trash.RestoreAsync(drywall.Id);
        var afterRestore = await _conflicts.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(whileTrashed, Is.Empty);
            Assert.That(afterRestore.Select(c => c.Blocker.Id), Is.EqualTo(new[] { drywall.Id }));
        });
    }

    [Test]
    public async Task ListAsync_FindsAConflictAcrossSpaces_NamingEachItemsSpace()
    {
        var garden = TestSpaces.Add(_db, "Garden");
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(6));
        var planting = await CreateAsync("Planting", Nov(3), Nov(8), garden);
        await _links.CreateAsync(drywall.Id, planting.Id, WorkItemLinkRelation.Blocks);

        var conflict = (await _conflicts.ListAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(conflict.Blocked.SpaceName, Is.EqualTo("Garden"));
            Assert.That(conflict.Blocker.SpaceName, Is.EqualTo("Home"));
        });
    }

    [Test]
    public async Task ListAsync_ForASpace_ListsConflictsWithEitherItemInIt()
    {
        var garden = TestSpaces.Add(_db, "Garden");
        var office = TestSpaces.Add(_db, "Office");
        var homeBlocker = await CreateAsync("Drywall", Nov(2), Nov(6));
        var gardenBlocked = await CreateAsync("Planting", Nov(3), Nov(8), garden);
        var gardenBlocker = await CreateAsync("Fence", Nov(2), Nov(6), garden);
        var homeBlocked = await CreateAsync("Paint", Nov(3), Nov(8));
        var officeBlocker = await CreateAsync("Desk", Nov(2), Nov(6), office);
        var officeBlocked = await CreateAsync("Chair", Nov(3), Nov(8), office);
        await _links.CreateAsync(homeBlocker.Id, gardenBlocked.Id, WorkItemLinkRelation.Blocks);
        await _links.CreateAsync(gardenBlocker.Id, homeBlocked.Id, WorkItemLinkRelation.Blocks);
        await _links.CreateAsync(officeBlocker.Id, officeBlocked.Id, WorkItemLinkRelation.Blocks);

        var conflicts = await _conflicts.ListAsync(garden.Id);

        Assert.That(conflicts.Select(c => c.Blocked.Id), Is.EquivalentTo(new[] { gardenBlocked.Id, homeBlocked.Id }));
    }

    [Test]
    public void ListAsync_ForASpaceThatDoesNotExist_IsRejected()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _conflicts.ListAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task ListAsync_ListsTheEarliestStartingBlockedItemFirst()
    {
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(10));
        var trim = await CreateAsync("Trim", Nov(7), Nov(8));
        var paint = await CreateAsync("Paint", Nov(4), Nov(8));
        await _links.CreateAsync(drywall.Id, trim.Id, WorkItemLinkRelation.Blocks);
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        var conflicts = await _conflicts.ListAsync();

        Assert.That(conflicts.Select(c => c.Blocked.Title), Is.EqualTo(new[] { "Paint", "Trim" }));
    }

    private static DateOnly Nov(int day) => new(2026, 11, day);

    private async Task<WorkItem> CreateAsync(string title, DateOnly? start, DateOnly? end, TestSpace? space = null)
    {
        var inSpace = space ?? _space;
        var item = await _workItems.CreateAsync(title, null, null, inSpace.ToDo, spaceId: inSpace.Id);
        if (start is null && end is null)
        {
            return item;
        }

        return (await _workItems.RescheduleAsync(item.Id, start, end)).Item;
    }
}
