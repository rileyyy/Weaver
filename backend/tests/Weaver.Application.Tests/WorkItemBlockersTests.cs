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
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), caller);
        _links = new WorkItemLinkService(_db, caller);
        _trash = new WorkItemTrashService(_db, new IterativeWorkItemHierarchy(_db), caller, _clock);
        _blockers = new WorkItemBlockers(_db);
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task BlockedAmongAsync_ReturnsOnlyTheItemsSomethingBlocks()
    {
        var drywall = await CreateAsync("Drywall");
        var paint = await CreateAsync("Paint");
        var related = await CreateAsync("Related");
        var duplicate = await CreateAsync("Duplicate");
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);
        await _links.CreateAsync(related.Id, paint.Id, WorkItemLinkRelation.Related);
        await _links.CreateAsync(duplicate.Id, drywall.Id, WorkItemLinkRelation.Duplicates);

        var blocked = await _blockers.BlockedAmongAsync([drywall.Id, paint.Id, related.Id, duplicate.Id]);

        Assert.That(blocked, Is.EquivalentTo(new[] { paint.Id }));
    }

    [Test]
    public async Task BlockedAmongAsync_IgnoresABlockerInTheTrash_UntilItIsRestored()
    {
        var drywall = await CreateAsync("Drywall");
        var paint = await CreateAsync("Paint");
        await _links.CreateAsync(paint.Id, drywall.Id, WorkItemLinkRelation.BlockedBy);

        await _trash.MoveToTrashAsync(drywall.Id);
        var whileTrashed = await _blockers.BlockedAmongAsync([paint.Id]);
        await _trash.RestoreAsync(drywall.Id);
        var afterRestore = await _blockers.BlockedAmongAsync([paint.Id]);

        Assert.Multiple(() =>
        {
            Assert.That(whileTrashed, Is.Empty);
            Assert.That(afterRestore, Is.EquivalentTo(new[] { paint.Id }));
        });
    }

    [Test]
    public async Task BlockedAmongAsync_WithNoIds_IsEmpty()
    {
        Assert.That(await _blockers.BlockedAmongAsync([]), Is.Empty);
    }

    private Task<WorkItem> CreateAsync(string title) =>
        _workItems.CreateAsync(title, null, null, _space.ToDo, spaceId: _space.Id);
}
