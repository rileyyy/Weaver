using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>The shared lookup rules are covered in <see cref="WorkItemPriorityServiceTests"/>.</summary>
[TestFixture]
public class WorkItemTypeServiceTests
{
    private WeaverDbContext _db = null!;
    private WorkItemTypeService _types = null!;
    private User _admin = null!;
    private TestSpace _space = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
        _types = new WorkItemTypeService(_db, new AdminAuthorizer(_db), FakeCallerContext.Anyone());
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetAllAsync_ReturnsSeededTypesOrderedByOrder()
    {
        var types = await _types.GetAllAsync(_space.Id);

        Assert.That(types.Select(l => l.Name), Is.EqualTo(new[] { "Project", "Goal", "Task" }));
    }

    [Test]
    public async Task GetAllAsync_GivesEverySeededTypeAColor()
    {
        var types = await _types.GetAllAsync(_space.Id);

        Assert.That(types.Select(l => l.Color), Is.EqualTo(new[] { "#D81B60", "#00897B", "#43A047" }));
    }

    [Test]
    public async Task CreateAndUpdate_SetTheNameAndColor()
    {
        var created = await _types.CreateAsync(_admin.Id, _space.Id, "Epic", "#546E7A");
        await _types.UpdateAsync(_admin.Id, created.Id, "Initiative", "#fdd835");

        var type = (await _types.GetAllAsync(_space.Id)).Last();
        Assert.Multiple(() =>
        {
            Assert.That(type.Name, Is.EqualTo("Initiative"));
            Assert.That(type.Color, Is.EqualTo("#FDD835"));
        });
    }

    [Test]
    public void CreateAsync_ByANonAdmin_IsForbidden()
    {
        var member = TestUsers.Add(_db, "member");

        Assert.ThrowsAsync<AdminRequiredException>(() => _types.CreateAsync(member.Id, _space.Id, "Epic", "#546E7A"));
    }

    [Test]
    public async Task DeleteAsync_WithAReplacement_MovesItsItemsToIt()
    {
        var item = AddItem(_space.Goal);

        await _types.DeleteAsync(_admin.Id, _space.Goal, _space.Task);

        Assert.That((await Reload(item)).TypeId, Is.EqualTo(_space.Task));
    }

    [Test]
    public async Task DeleteAsync_WithoutAReplacement_LeavesItsItemsWithNoType()
    {
        var item = AddItem(_space.Goal);

        await _types.DeleteAsync(_admin.Id, _space.Goal, replacementId: null);

        Assert.That((await Reload(item)).TypeId, Is.Null);
    }

    private WorkItem AddItem(Guid typeId)
    {
        var item = WorkItem.Create(_space.Id, "Item", null, _db.Statuses.Find(_space.ToDo)!, 1, FixedServerCalendar.DefaultToday, typeId: typeId);
        _db.WorkItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private Task<WorkItem> Reload(WorkItem item) =>
        _db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item.Id);
}
