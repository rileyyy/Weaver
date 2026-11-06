using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// Moving a work item and its subtree into another Space (ADR-0001, ticket 16). Home has the
/// default configuration; the Workshop has its own, so defaults have to be worked out.
/// </summary>
[TestFixture]
public class WorkItemSpaceMoveServiceTests
{
    private WeaverDbContext _db = null!;
    private WorkItemService _workItems = null!;
    private WorkItemSpaceMoveService _moves = null!;
    private WorkItemHistoryService _history = null!;
    private User _alice = null!;
    private TestSpace _home = null!;
    private TestWorkshop _workshop = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(new FixedTimeProvider(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero)));
        _alice = TestUsers.Add(_db, "alice");
        _home = TestSpaces.Add(_db, "Home");
        _workshop = TestWorkshop.Add(_db);
        var caller = new FakeCallerContext(_alice.Id);
        var hierarchy = new IterativeWorkItemHierarchy(_db);
        _workItems = new WorkItemService(_db, hierarchy, caller);
        _moves = new WorkItemSpaceMoveService(_db, hierarchy, caller);
        _history = new WorkItemHistoryService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task PlanAsync_ListsEachValueTheSubtreeUses_WithItsDefault()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, _home.Task, _home.High);
        await CreateAsync("Roof", root.Id, _home.Doing, _home.Project, _home.Low);
        await CreateAsync("Door", root.Id, _home.Doing, _home.Task, null);

        var plan = await _moves.PlanAsync(root.Id, _workshop.Id);

        Assert.Multiple(() =>
        {
            Assert.That(plan.ItemCount, Is.EqualTo(3));
            Assert.That(
                plan.Statuses.Select(m => (m.SourceName, m.TargetId, m.ItemCount)),
                Is.EquivalentTo(new[] { ("To Do", (Guid?)_workshop.Quoted, 1), ("Doing", _workshop.Booked, 2) }));
            Assert.That(
                plan.Types.Select(m => (m.SourceName, m.TargetId, m.ItemCount)),
                Is.EquivalentTo(new[] { ("Task", (Guid?)_workshop.Task, 2), ("Project", null, 1) }));
            Assert.That(
                plan.Priorities.Select(m => (m.SourceName, m.TargetId)),
                Is.EquivalentTo(new[] { ("High", (Guid?)_workshop.High), ("Low", null) }));
        });
    }

    [Test]
    public async Task PlanAsync_DefaultsAStatusToTheOneWithItsName_BeforeItsCategory()
    {
        var root = await CreateAsync("Shed", null, _home.Done, null, null);

        var plan = await _moves.PlanAsync(root.Id, _workshop.Id);

        Assert.That(plan.Statuses.Single().TargetId, Is.EqualTo(_workshop.Done));
    }

    [Test]
    public async Task PlanAsync_DefaultsAStatusWithNoMatchInItsCategory_ToTheFirstStatus()
    {
        var parked = Status.Create(_home.Id, "Parked", 3, StatusCategory.Doing, "#000000");
        _db.Statuses.Add(parked);
        _db.Statuses.Remove(_db.Statuses.Single(s => s.Id == _workshop.Booked));
        await _db.SaveChangesAsync();
        var root = await CreateAsync("Shed", null, parked.Id, null, null);

        var plan = await _moves.PlanAsync(root.Id, _workshop.Id);

        Assert.That(plan.Statuses.Single().TargetId, Is.EqualTo(_workshop.Quoted));
    }

    [Test]
    public async Task PlanAsync_IncludesSubItemsInTheTrash_SinceTheyMoveToo()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, null, null);
        var trashed = await CreateAsync("Roof", root.Id, _home.Doing, null, null);
        await new WorkItemTrashService(
                _db, new IterativeWorkItemHierarchy(_db), new FakeCallerContext(_alice.Id), TimeProvider.System)
            .MoveToTrashAsync(trashed.Id);

        var plan = await _moves.PlanAsync(root.Id, _workshop.Id);

        Assert.Multiple(() =>
        {
            Assert.That((plan.ItemCount, plan.TrashedItemCount), Is.EqualTo((2, 1)));
            Assert.That(plan.Statuses.Select(m => m.SourceName), Is.EquivalentTo(new[] { "To Do", "Doing" }));
        });
    }

    [Test]
    public async Task MoveAsync_IntoASpaceWithNoStatuses_IsRejected()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, null, null);
        _db.Statuses.RemoveRange(_db.Statuses.Where(s => s.SpaceId == _workshop.Id));
        await _db.SaveChangesAsync();

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _moves.MoveAsync(root.Id, _workshop.Id, null, SpaceMoveMaps.AllDefaults));
    }

    [Test]
    public async Task PlanAsync_CountsTheCustomFieldValuesTheMoveClears()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, _home.Project, null);
        var budget = AddField(_home.Project, "Budget");
        await _workItems.SetCustomFieldsAsync(root.Id, Values((budget, "100")));

        var plan = await _moves.PlanAsync(root.Id, _workshop.Id);

        Assert.That(plan.ClearedFieldValueCount, Is.EqualTo(1));
    }

    [Test]
    public async Task MoveAsync_WithoutMaps_MovesTheWholeSubtreeOnTheDefaults()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, _home.Task, _home.High);
        var child = await CreateAsync("Roof", root.Id, _home.Doing, _home.Project, _home.Low);
        var grandchild = await CreateAsync("Tiles", child.Id, _home.Done, null, null);

        await _moves.MoveAsync(root.Id, _workshop.Id, targetParentId: null, SpaceMoveMaps.AllDefaults);

        var (movedRoot, movedChild, movedGrandchild) = (await ReloadAsync(root), await ReloadAsync(child), await ReloadAsync(grandchild));
        Assert.Multiple(() =>
        {
            Assert.That(new[] { movedRoot.SpaceId, movedChild.SpaceId, movedGrandchild.SpaceId }, Is.All.EqualTo(_workshop.Id));
            Assert.That((movedRoot.StatusId, movedRoot.TypeId, movedRoot.PriorityId), Is.EqualTo((_workshop.Quoted, (Guid?)_workshop.Task, (Guid?)_workshop.High)));
            Assert.That((movedChild.StatusId, movedChild.TypeId, movedChild.PriorityId), Is.EqualTo((_workshop.Booked, (Guid?)null, (Guid?)null)));
            Assert.That(movedGrandchild.StatusId, Is.EqualTo(_workshop.Done));
            Assert.That(movedRoot.ParentId, Is.Null);
            Assert.That(movedChild.ParentId, Is.EqualTo(root.Id));
            Assert.That(movedGrandchild.ParentId, Is.EqualTo(child.Id));
        });
    }

    [Test]
    public async Task MoveAsync_TheMapsOverrideTheDefaults()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, _home.Task, _home.High);
        var maps = new SpaceMoveMaps(
            Statuses: new Dictionary<Guid, Guid> { [_home.ToDo] = _workshop.Done },
            Types: new Dictionary<Guid, Guid?> { [_home.Task] = null },
            Priorities: new Dictionary<Guid, Guid?> { [_home.High] = _workshop.Whenever });

        var result = await _moves.MoveAsync(root.Id, _workshop.Id, null, maps);

        Assert.That(
            (result.Item.StatusId, result.Item.TypeId, result.Item.PriorityId),
            Is.EqualTo((_workshop.Done, (Guid?)null, (Guid?)_workshop.Whenever)));
    }

    [Test]
    public async Task MoveAsync_UnderATargetParent_PlacesTheRootThere()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, null, null);
        var bench = await _workItems.CreateAsync("Bench", null, null, _workshop.Quoted, spaceId: _workshop.Id);

        var result = await _moves.MoveAsync(root.Id, _workshop.Id, bench.Id, SpaceMoveMaps.AllDefaults);

        Assert.Multiple(() =>
        {
            Assert.That(result.Item.ParentId, Is.EqualTo(bench.Id));
            Assert.That(result.ChangedFields, Is.SupersetOf(new[] { WorkItemField.SpaceId, WorkItemField.ParentId, WorkItemField.StatusId }));
        });
    }

    [Test]
    public async Task MoveAsync_UnderAParentInAnotherSpace_IsRejectedAndChangesNothing()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, null, null);
        var homeItem = await CreateAsync("Kitchen", null, _home.ToDo, null, null);

        Assert.ThrowsAsync<CrossSpaceParentException>(() =>
            _moves.MoveAsync(root.Id, _workshop.Id, homeItem.Id, SpaceMoveMaps.AllDefaults));
        Assert.That((await ReloadAsync(root)).SpaceId, Is.EqualTo(_home.Id));
    }

    [Test]
    public async Task MoveAsync_IntoItsOwnSpace_IsRejected()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, null, null);

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _moves.MoveAsync(root.Id, _home.Id, null, SpaceMoveMaps.AllDefaults));
    }

    [Test]
    public void MoveAsync_IntoAnUnknownSpace_ThrowsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(async () =>
        {
            var root = await CreateAsync("Shed", null, _home.ToDo, null, null);
            await _moves.MoveAsync(root.Id, Guid.NewGuid(), null, SpaceMoveMaps.AllDefaults);
        });
    }

    [Test]
    public async Task MoveAsync_MappingToAnotherSpacesStatus_IsRejectedAndChangesNothing()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, null, null);
        var maps = SpaceMoveMaps.AllDefaults with { Statuses = new Dictionary<Guid, Guid> { [_home.ToDo] = _home.Doing } };

        Assert.ThrowsAsync<DomainValidationException>(() => _moves.MoveAsync(root.Id, _workshop.Id, null, maps));
        Assert.That((await ReloadAsync(root)).SpaceId, Is.EqualTo(_home.Id));
    }

    [Test]
    public async Task MoveAsync_MappingFromAValueOutsideTheItemsSpace_IsRejected()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, null, null);
        var maps = SpaceMoveMaps.AllDefaults with { Types = new Dictionary<Guid, Guid?> { [_workshop.Task] = _workshop.Chore } };

        Assert.ThrowsAsync<DomainValidationException>(() => _moves.MoveAsync(root.Id, _workshop.Id, null, maps));
    }

    [Test]
    public async Task MoveAsync_WithAStaleVersion_IsRejected()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, null, null);

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() =>
            _moves.MoveAsync(root.Id, _workshop.Id, null, SpaceMoveMaps.AllDefaults, expectedVersion: root.Version + 1));
    }

    [Test]
    public async Task MoveAsync_RecordsTheMoveInEveryMovedItemsHistory_AttributedToTheCaller()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, null, null);
        var child = await CreateAsync("Roof", root.Id, _home.ToDo, null, null);

        await _moves.MoveAsync(root.Id, _workshop.Id, null, SpaceMoveMaps.AllDefaults);

        foreach (var item in new[] { root, child })
        {
            var entries = await _history.ListForWorkItemAsync(item.Id);
            var spaceMove = entries.First(e => e.Field == WorkItemField.SpaceId);
            Assert.Multiple(() =>
            {
                Assert.That(spaceMove.OldName, Is.EqualTo("Home"));
                Assert.That(spaceMove.NewName, Is.EqualTo("Workshop"));
                Assert.That(spaceMove.AccountId, Is.EqualTo(_alice.Id));
                Assert.That(entries.Any(e => e.Field == WorkItemField.StatusId && e.NewName == "Quoted"), Is.True);
            });
        }
    }

    [Test]
    public async Task MoveAsync_ClearsCustomFieldValues_AndHistoryKeepsThem()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, _home.Project, null);
        var budget = AddField(_home.Project, "Budget");
        await _workItems.SetCustomFieldsAsync(root.Id, Values((budget, "100")));

        await _moves.MoveAsync(root.Id, _workshop.Id, null, SpaceMoveMaps.AllDefaults);

        var cleared = (await _history.ListForWorkItemAsync(root.Id)).First(e => e.Field == budget.ChangeName);
        Assert.Multiple(async () =>
        {
            Assert.That((await ReloadAsync(root)).CustomFields, Is.Empty);
            Assert.That(cleared.OldValue?.GetRawText(), Is.EqualTo("100"));
        });
    }

    [Test]
    public async Task MoveAsync_KeepsLinks_IncludingOnesToOtherSpaces()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, null, null);
        var kitchen = await CreateAsync("Kitchen", null, _home.ToDo, null, null);
        await new WorkItemLinkService(_db, new FakeCallerContext(_alice.Id))
            .CreateAsync(root.Id, kitchen.Id, WorkItemLinkRelation.Blocks);

        await _moves.MoveAsync(root.Id, _workshop.Id, null, SpaceMoveMaps.AllDefaults);

        Assert.That(
            _db.WorkItemLinks.Count(l => l.WorkItemId == root.Id || l.LinkedWorkItemId == root.Id),
            Is.EqualTo(1));
    }

    [Test]
    public async Task MoveAsync_TakesASubItemInTheTrashAlong_SoRestoringItKeepsItsParentsSpace()
    {
        var root = await CreateAsync("Shed", null, _home.ToDo, null, null);
        var trashedChild = await CreateAsync("Roof", root.Id, _home.Doing, null, null);
        var trash = new WorkItemTrashService(
            _db, new IterativeWorkItemHierarchy(_db), new FakeCallerContext(_alice.Id), TimeProvider.System);
        await trash.MoveToTrashAsync(trashedChild.Id);

        await _moves.MoveAsync(root.Id, _workshop.Id, null, SpaceMoveMaps.AllDefaults);

        var moved = await _db.WorkItems.IgnoreQueryFilters().AsNoTracking().SingleAsync(w => w.Id == trashedChild.Id);
        Assert.Multiple(() =>
        {
            Assert.That(moved.SpaceId, Is.EqualTo(_workshop.Id));
            Assert.That(moved.StatusId, Is.EqualTo(_workshop.Booked));
            Assert.That(moved.IsTrashed, Is.True);
        });
    }

    [Test]
    public async Task MoveAsync_LeavesItemsOutsideTheSubtreeAlone()
    {
        var parent = await CreateAsync("House", null, _home.ToDo, null, null);
        var moved = await CreateAsync("Shed", parent.Id, _home.ToDo, null, null);
        var sibling = await CreateAsync("Garage", parent.Id, _home.ToDo, null, null);

        await _moves.MoveAsync(moved.Id, _workshop.Id, null, SpaceMoveMaps.AllDefaults);

        Assert.Multiple(async () =>
        {
            Assert.That((await ReloadAsync(parent)).SpaceId, Is.EqualTo(_home.Id));
            Assert.That((await ReloadAsync(sibling)).SpaceId, Is.EqualTo(_home.Id));
            Assert.That((await ReloadAsync(moved)).ParentId, Is.Null);
        });
    }

    private async Task<WorkItem> CreateAsync(string title, Guid? parentId, Guid statusId, Guid? typeId, Guid? priorityId) =>
        await _workItems.CreateAsync(
            title, null, parentId, statusId, typeId: typeId, priorityId: priorityId, spaceId: parentId is null ? _home.Id : null);

    private Task<WorkItem> ReloadAsync(WorkItem item) =>
        _db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item.Id);

    private CustomFieldDefinition AddField(Guid typeId, string name)
    {
        var field = CustomFieldDefinition.Create(typeId, name, CustomFieldKind.Number, order: 0);
        _db.CustomFields.Add(field);
        _db.SaveChanges();
        return field;
    }

    private static IReadOnlyDictionary<Guid, JsonElement> Values(params (CustomFieldDefinition Field, string Json)[] values) =>
        values.ToDictionary(v => v.Field.Id, v => JsonSerializer.Deserialize<JsonElement>(v.Json));
}
