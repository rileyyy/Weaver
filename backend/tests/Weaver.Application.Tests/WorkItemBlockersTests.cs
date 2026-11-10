using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemBlockersTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private WorkItemService _workItems = null!;
    private WorkItemLinkService _links = null!;
    private WorkItemTrashService _trash = null!;
    private WorkItemBlockers _blockers = null!;
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
        _blockers = new WorkItemBlockers(_db);
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task BlockersOfAsync_ListsOnlyTheItemsSomethingBlocks()
    {
        var drywall = await CreateAsync("Drywall");
        var paint = await CreateAsync("Paint");
        var related = await CreateAsync("Related");
        var duplicate = await CreateAsync("Duplicate");
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);
        await _links.CreateAsync(related.Id, paint.Id, WorkItemLinkRelation.Related);
        await _links.CreateAsync(duplicate.Id, drywall.Id, WorkItemLinkRelation.Duplicates);

        var blockers = await _blockers.BlockersOfAsync([drywall.Id, paint.Id, related.Id, duplicate.Id]);

        Assert.Multiple(() =>
        {
            Assert.That(blockers.Keys, Is.EquivalentTo(new[] { paint.Id }));
            Assert.That(blockers[paint.Id], Is.EqualTo(new[] { new WorkItemBlocker(drywall.Id, drywall.Number, "Drywall", false) }));
        });
    }

    [Test]
    public async Task BlockersOfAsync_ListsEveryBlocker_SayingWhichOnesConflict()
    {
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(6));
        var wiring = await CreateAsync("Wiring", Nov(1), Nov(3));
        var paint = await CreateAsync("Paint", Nov(4), Nov(8));
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);
        await _links.CreateAsync(wiring.Id, paint.Id, WorkItemLinkRelation.Blocks);

        var blockers = await _blockers.BlockersOfAsync([paint.Id]);

        Assert.That(blockers[paint.Id].Select(b => (b.Title, b.Conflicts)), Is.EquivalentTo(new[]
        {
            ("Drywall", true),
            ("Wiring", false),
        }));
    }

    [Test]
    public async Task BlockersOfAsync_KeepsADoneBlocker_ButNotAsAConflict()
    {
        var drywall = await CreateAsync("Drywall", Nov(2), Nov(6));
        var paint = await CreateAsync("Paint", Nov(4), Nov(8));
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);
        await _workItems.ChangeStatusAsync(drywall.Id, _space.Done);

        var blocker = (await _blockers.BlockersOfAsync([paint.Id]))[paint.Id].Single();

        Assert.Multiple(() =>
        {
            Assert.That(blocker.Id, Is.EqualTo(drywall.Id));
            Assert.That(blocker.Conflicts, Is.False);
        });
    }

    [Test]
    public async Task BlockersOfAsync_IgnoresABlockerInTheTrash_UntilItIsRestored()
    {
        var drywall = await CreateAsync("Drywall");
        var paint = await CreateAsync("Paint");
        await _links.CreateAsync(paint.Id, drywall.Id, WorkItemLinkRelation.BlockedBy);

        await _trash.MoveToTrashAsync(drywall.Id);
        var whileTrashed = await _blockers.BlockersOfAsync([paint.Id]);
        await _trash.RestoreAsync(drywall.Id);
        var afterRestore = await _blockers.BlockersOfAsync([paint.Id]);

        Assert.Multiple(() =>
        {
            Assert.That(whileTrashed, Is.Empty);
            Assert.That(afterRestore.Keys, Is.EquivalentTo(new[] { paint.Id }));
        });
    }

    [Test]
    public async Task BlockersOfAsync_WithNoIds_IsEmpty()
    {
        Assert.That(await _blockers.BlockersOfAsync([]), Is.Empty);
    }

    private static DateOnly Nov(int day) => new(2026, 11, day);

    private async Task<WorkItem> CreateAsync(string title, DateOnly start, DateOnly end)
    {
        var item = await CreateAsync(title);
        return (await _workItems.RescheduleAsync(item.Id, start, end)).Item;
    }

    private Task<WorkItem> CreateAsync(string title) =>
        _workItems.CreateAsync(title, null, null, _space.ToDo, spaceId: _space.Id);
}
