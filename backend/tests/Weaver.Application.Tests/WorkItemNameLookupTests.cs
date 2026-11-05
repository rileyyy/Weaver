using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemNameLookupTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private WorkItemService _workItems = null!;
    private WorkItemTrashService _trash = null!;
    private WorkItemNameLookup _lookup = null!;
    private TestSpace _home = null!;
    private TestSpace _garden = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        var caller = FakeCallerContext.Anyone();
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), caller);
        _trash = new WorkItemTrashService(_db, new IterativeWorkItemHierarchy(_db), caller, _clock);
        _lookup = new WorkItemNameLookup(_db);
        _home = TestSpaces.Add(_db, "Home");
        _garden = TestSpaces.Add(_db, "Garden");
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task NamesForAsync_NamesEveryIdTheItemsHold()
    {
        var riley = TestUsers.Add(_db, "riley");
        var room = await _workItems.CreateAsync("Living room", null, null, _home.Doing, spaceId: _home.Id);
        var paint = await _workItems.CreateAsync("Paint", null, room.Id, _home.ToDo, typeId: _home.Task, priorityId: _home.High);
        paint.AssignTo(riley.Id);
        await _db.SaveChangesAsync();
        var weeds = await _workItems.CreateAsync("Weeds", null, null, _garden.Done, spaceId: _garden.Id);

        var names = await _lookup.NamesForAsync([room, paint, weeds]);

        Assert.Multiple(() =>
        {
            Assert.That(names.SpaceName(_home.Id), Is.EqualTo("Home"));
            Assert.That(names.SpaceName(_garden.Id), Is.EqualTo("Garden"));
            Assert.That(names.StatusName(_home.ToDo), Is.EqualTo("To Do"));
            Assert.That(names.StatusName(_home.Doing), Is.EqualTo("Doing"));
            Assert.That(names.StatusName(_garden.Done), Is.EqualTo("Done"));
            Assert.That(names.TypeName(_home.Task), Is.EqualTo("Task"));
            Assert.That(names.PriorityName(_home.High), Is.EqualTo("High"));
            Assert.That(names.Username(riley.Id), Is.EqualTo("riley"));
            Assert.That(names.WorkItem(room.Id), Is.EqualTo(new WorkItemReference(room.Number, "Living room")));
        });
    }

    [Test]
    public async Task NamesForAsync_NamesAParentInTheTrash()
    {
        var room = await _workItems.CreateAsync("Living room", null, null, _home.ToDo, spaceId: _home.Id);
        var paint = await _workItems.CreateAsync("Paint", null, room.Id, _home.ToDo);
        await _trash.MoveToTrashAsync(room.Id, cascade: true);

        var names = await _lookup.NamesForAsync([paint]);

        Assert.That(names.WorkItem(room.Id)?.Title, Is.EqualTo("Living room"));
    }

    [Test]
    public async Task NamesForAsync_LooksUpOnlyTheIdsTheItemsHold()
    {
        var item = await _workItems.CreateAsync("Weeds", null, null, _garden.ToDo, spaceId: _garden.Id);

        var names = await _lookup.NamesForAsync([item]);

        Assert.Multiple(() =>
        {
            Assert.That(names.Spaces.Keys, Is.EqualTo(new[] { _garden.Id }));
            Assert.That(names.Statuses.Keys, Is.EqualTo(new[] { _garden.ToDo }));
            Assert.That(names.Types, Is.Empty);
            Assert.That(names.Priorities, Is.Empty);
            Assert.That(names.Usernames, Is.Empty);
            Assert.That(names.WorkItems, Is.Empty);
        });
    }

    [Test]
    public async Task NamesForAsync_WithNoItems_HasNoNames()
    {
        var names = await _lookup.NamesForAsync([]);

        Assert.That(names.Spaces, Is.Empty);
    }
}
