using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Configurations;

namespace Weaver.Application.Tests;

/// <summary>The shared lookup rules are covered in <see cref="WorkItemPriorityServiceTests"/>.</summary>
[TestFixture]
public class StatusServiceTests
{
    private WeaverDbContext _db = null!;
    private StatusService _statuses = null!;
    private User _admin = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
        _statuses = new StatusService(_db, new AdminAuthorizer(_db));
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetAllAsync_ReturnsSeededStatusesOrderedByOrder()
    {
        var statuses = await _statuses.GetAllAsync();

        Assert.That(statuses.Select(s => s.Name), Is.EqualTo(new[] { "To Do", "Doing", "Done" }));
    }

    [Test]
    public async Task CreateAsync_StoresCategoryAndColorAndAppendsIt()
    {
        var created = await _statuses.CreateAsync(_admin.Id, "Review", StatusCategory.Doing, "#00897b");

        var last = (await _statuses.GetAllAsync()).Last();
        Assert.Multiple(() =>
        {
            Assert.That(last.Id, Is.EqualTo(created.Id));
            Assert.That(last.Category, Is.EqualTo(StatusCategory.Doing));
            Assert.That(last.Color, Is.EqualTo("#00897B"));
        });
    }

    [Test]
    public void CreateAsync_WithAnInvalidColor_IsAValidationError()
    {
        Assert.ThrowsAsync<DomainValidationException>(() =>
            _statuses.CreateAsync(_admin.Id, "Review", StatusCategory.Doing, "teal"));
    }

    [Test]
    public void CreateAsync_ByANonAdmin_IsForbidden()
    {
        var member = TestUsers.Add(_db, "member");

        Assert.ThrowsAsync<AdminRequiredException>(() =>
            _statuses.CreateAsync(member.Id, "Review", StatusCategory.Doing, "#00897B"));
    }

    [Test]
    public async Task UpdateAsync_ChangesNameCategoryAndColor()
    {
        await _statuses.UpdateAsync(_admin.Id, StatusConfiguration.DoingId, "In progress", StatusCategory.ToDo, "#123456");

        var doing = await _db.Statuses.AsNoTracking().SingleAsync(s => s.Id == StatusConfiguration.DoingId);
        Assert.Multiple(() =>
        {
            Assert.That(doing.Name, Is.EqualTo("In progress"));
            Assert.That(doing.Category, Is.EqualTo(StatusCategory.ToDo));
            Assert.That(doing.Color, Is.EqualTo("#123456"));
        });
    }

    [Test]
    public async Task DeleteAsync_InUseWithoutAReplacement_IsAConflictAndChangesNothing()
    {
        var item = AddItem(null, StatusConfiguration.DoingId, 1);

        Assert.ThrowsAsync<LookupValueInUseException>(() =>
            _statuses.DeleteAsync(_admin.Id, StatusConfiguration.DoingId, replacementId: null));

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _db.Statuses.CountAsync(), Is.EqualTo(3));
            Assert.That((await Reload(item)).StatusId, Is.EqualTo(StatusConfiguration.DoingId));
        });
    }

    [Test]
    public async Task DeleteAsync_UnusedWithoutAReplacement_Deletes()
    {
        await _statuses.DeleteAsync(_admin.Id, StatusConfiguration.DoingId, replacementId: null);

        Assert.That(await _db.Statuses.AnyAsync(s => s.Id == StatusConfiguration.DoingId), Is.False);
    }

    [Test]
    public async Task DeleteAsync_WithAReplacement_AppendsMovedItemsToEachLanesColumnInOrder()
    {
        var lane = AddItem(null, StatusConfiguration.ToDoId, 1);
        var existingDone = AddItem(lane.Id, StatusConfiguration.DoneId, 5);
        var firstDoing = AddItem(lane.Id, StatusConfiguration.DoingId, 1);
        var secondDoing = AddItem(lane.Id, StatusConfiguration.DoingId, 2);
        var otherLane = AddItem(null, StatusConfiguration.ToDoId, 2);
        var otherLaneDoing = AddItem(otherLane.Id, StatusConfiguration.DoingId, 1);

        await _statuses.DeleteAsync(_admin.Id, StatusConfiguration.DoingId, StatusConfiguration.DoneId);

        var first = await Reload(firstDoing);
        var second = await Reload(secondDoing);
        var other = await Reload(otherLaneDoing);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(new[] { first.StatusId, second.StatusId, other.StatusId }, Is.All.EqualTo(StatusConfiguration.DoneId));
            Assert.That(first.ParentId, Is.EqualTo(lane.Id));
            Assert.That(other.ParentId, Is.EqualTo(otherLane.Id));
            Assert.That(first.Rank, Is.GreaterThan((await Reload(existingDone)).Rank));
            Assert.That(second.Rank, Is.GreaterThan(first.Rank));
            Assert.That(other.Rank, Is.EqualTo(1).Within(double.Epsilon), "an empty cell starts at the first rank");
        });
    }

    [Test]
    public async Task DeleteAsync_TheLastStatus_IsAConflict()
    {
        await _statuses.DeleteAsync(_admin.Id, StatusConfiguration.DoingId, null);
        await _statuses.DeleteAsync(_admin.Id, StatusConfiguration.DoneId, null);

        Assert.ThrowsAsync<LastLookupValueException>(() => _statuses.DeleteAsync(_admin.Id, StatusConfiguration.ToDoId, null));
    }

    private WorkItem AddItem(Guid? parentId, Guid statusId, double rank)
    {
        var item = WorkItem.Create("Item", null, parentId, statusId, rank);
        _db.WorkItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private Task<WorkItem> Reload(WorkItem item) =>
        _db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item.Id);
}
