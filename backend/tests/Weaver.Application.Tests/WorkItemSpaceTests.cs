using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Configurations;

namespace Weaver.Application.Tests;

/// <summary>Which Space work items land in, and the rule that reparenting never leaves it.</summary>
[TestFixture]
public class WorkItemSpaceTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private WorkItemService _service = null!;
    private Space _home = null!;
    private Space _village = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _service = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), FakeCallerContext.Anyone());
        _home = TestSpaces.Add(_db, "Home");
        _village = TestSpaces.Add(_db, "Village");
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task CreateAsync_ATopLevelItem_LandsInTheChosenSpace()
    {
        var item = await CreateAsync("Fix the fence", space: _village);

        Assert.That(item.SpaceId, Is.EqualTo(_village.Id));
    }

    [Test]
    public void CreateAsync_ATopLevelItemWithoutASpace_IsRejected()
    {
        Assert.ThrowsAsync<DomainValidationException>(() =>
            _service.CreateAsync("Nowhere", null, null, StatusConfiguration.ToDoId));
    }

    [Test]
    public void CreateAsync_InAnUnknownSpace_ThrowsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.CreateAsync("Lost", null, null, StatusConfiguration.ToDoId, spaceId: Guid.NewGuid()));
    }

    [Test]
    public async Task CreateAsync_AChild_InheritsItsParentsSpace()
    {
        var parent = await CreateAsync("Fence", space: _village);

        var child = await _service.CreateAsync("Paint it", null, parent.Id, StatusConfiguration.ToDoId);

        Assert.That(child.SpaceId, Is.EqualTo(_village.Id));
    }

    [Test]
    public async Task CreateAsync_AChildNamingAnotherSpace_IsRejected()
    {
        var parent = await CreateAsync("Fence", space: _village);

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _service.CreateAsync("Paint it", null, parent.Id, StatusConfiguration.ToDoId, spaceId: _home.Id));
    }

    [Test]
    public async Task ReparentAsync_UnderAnItemInAnotherSpace_IsRejectedAndChangesNothing()
    {
        var item = await CreateAsync("Kitchen", space: _home);
        var elsewhere = await CreateAsync("Fence", space: _village);

        Assert.ThrowsAsync<CrossSpaceParentException>(() => _service.ReparentAsync(item.Id, elsewhere.Id));

        _db.ChangeTracker.Clear();
        var reloaded = await _db.WorkItems.SingleAsync(w => w.Id == item.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.ParentId, Is.Null);
            Assert.That(reloaded.SpaceId, Is.EqualTo(_home.Id));
            Assert.That(_db.WorkItemHistory.Count(e => e.WorkItemId == item.Id && e.Field == WorkItemField.ParentId), Is.Zero);
        });
    }

    [Test]
    public async Task ReparentAsync_WithinTheSpace_StillWorks()
    {
        var item = await CreateAsync("Kitchen", space: _home);
        var parent = await CreateAsync("Renovation", space: _home);

        var moved = (await _service.ReparentAsync(item.Id, parent.Id)).Item;

        Assert.That(moved.ParentId, Is.EqualTo(parent.Id));
    }

    [Test]
    public async Task GetChildrenAsync_AtTopLevel_ListsOnlyThatSpacesItems()
    {
        var kitchen = await CreateAsync("Kitchen", space: _home);
        await CreateAsync("Fence", space: _village);

        var topLevel = await _service.GetChildrenAsync(parentId: null, spaceId: _home.Id);

        Assert.That(topLevel.Select(w => w.Id), Is.EqualTo(new[] { kitchen.Id }));
    }

    [Test]
    public void GetChildrenAsync_WithNeitherAParentNorASpace_IsRejected()
    {
        Assert.ThrowsAsync<DomainValidationException>(() => _service.GetChildrenAsync(parentId: null));
    }

    [Test]
    public async Task GetSwimlanesAsync_AtTopLevel_ShowsOnlyThatSpacesLanes()
    {
        var kitchen = await CreateAsync("Kitchen", space: _home);
        await CreateAsync("Fence", space: _village);

        var lanes = await _service.GetSwimlanesAsync(scopeItemId: null, spaceId: _home.Id);

        Assert.That(lanes.Select(l => l.Lane.Id), Is.EqualTo(new[] { kitchen.Id }));
    }

    [Test]
    public async Task GetAllAsync_WithASpace_ListsEveryItemInItAndNoOthers()
    {
        var kitchen = await CreateAsync("Kitchen", space: _home);
        var sink = await _service.CreateAsync("Sink", null, kitchen.Id, StatusConfiguration.ToDoId);
        await CreateAsync("Fence", space: _village);

        var all = await _service.GetAllAsync(0, 100, spaceId: _home.Id);

        Assert.That(all.Select(w => w.Id), Is.EquivalentTo(new[] { kitchen.Id, sink.Id }));
    }

    [Test]
    public async Task CreateAsync_AfterAnItemInAnotherSpacesTopLevel_ThrowsNotFound()
    {
        var fence = await CreateAsync("Fence", space: _village);

        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.CreateAsync("Kitchen", null, null, StatusConfiguration.ToDoId, afterId: fence.Id, spaceId: _home.Id));
    }

    [Test]
    public async Task RepeatingItem_CopiesAreCreatedInTheTemplatesSpace()
    {
        var calendar = new ServerCalendar(new ServerSettingsService(_db, new AdminAuthorizer(_db)), _clock);
        var recurrences = new WorkItemRecurrenceService(
            _db, calendar, NullLogger<WorkItemRecurrenceService>.Instance, FakeCallerContext.Anyone());
        var market = await CreateAsync("Market stall", space: _village);
        var template = await _service.CreateAsync("Set up", null, market.Id, StatusConfiguration.ToDoId);
        await _service.CreateAsync("Bring the float", null, template.Id, StatusConfiguration.ToDoId);

        await recurrences.SetAsync(template.Id, RecurrenceFrequency.Weekly, [DayOfWeek.Saturday], new DateOnly(2026, 9, 26), null);

        // Only the generated copies are scheduled: the template and its sub-item have no dates.
        var copies = await _db.WorkItems.AsNoTracking().Where(w => w.StartDate != null).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(copies.Select(w => w.Title), Does.Contain("Set up").And.Contain("Bring the float"));
            Assert.That(copies.Select(w => w.SpaceId), Is.All.EqualTo(_village.Id));
        });
    }

    private Task<WorkItem> CreateAsync(string title, Space space) =>
        _service.CreateAsync(title, null, null, StatusConfiguration.ToDoId, spaceId: space.Id);
}
