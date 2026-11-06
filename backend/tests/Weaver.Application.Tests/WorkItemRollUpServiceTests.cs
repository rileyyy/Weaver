using System.Text.Json;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemRollUpServiceTests
{
    private WeaverDbContext _db = null!;
    private WorkItemService _workItems = null!;
    private WorkItemTrashService _trash = null!;
    private WorkItemRollUpService _rollUps = null!;
    private TestSpace _home = null!;
    private CustomFieldDefinition _projectBudget = null!;
    private CustomFieldDefinition _taskHours = null!;

    [SetUp]
    public void SetUp()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(clock);
        var alice = TestUsers.Add(_db, "alice");
        _home = TestSpaces.Add(_db);
        _projectBudget = AddField(_home.Project, "Budget", CustomFieldKind.Currency, rollsUp: true);
        _taskHours = AddField(_home.Task, "Hours", CustomFieldKind.Number, rollsUp: true);
        var hierarchy = new IterativeWorkItemHierarchy(_db);
        var caller = new FakeCallerContext(alice.Id);
        _workItems = new WorkItemService(_db, hierarchy, caller);
        _trash = new WorkItemTrashService(_db, hierarchy, caller, clock);
        _rollUps = new WorkItemRollUpService(_db, hierarchy);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetAsync_SumsEveryDescendantAtAnyDepth_ThroughItemsOfOtherTypes()
    {
        var house = await CreateAsync(null, _home.Project, (_projectBudget, "1000"));
        var kitchen = await CreateAsync(house, _home.Goal);
        await CreateAsync(kitchen, _home.Project, (_projectBudget, "250.50"));
        await CreateAsync(kitchen, _home.Task, (_taskHours, "3"));
        var tiling = await CreateAsync(house, _home.Task, (_taskHours, "4.5"));
        await CreateAsync(tiling, _home.Project, (_projectBudget, "100"));

        var totals = await _rollUps.GetAsync(house.Id);

        Assert.That(
            totals.Select(t => (t.Field.Id, t.Total)),
            Is.EqualTo(new[] { (_projectBudget.Id, 350.50m), (_taskHours.Id, 7.5m) }));
    }

    [Test]
    public async Task GetAsync_ListsTheItemsOwnTypesFieldsFirst()
    {
        var chore = await CreateAsync(null, _home.Task);
        await CreateAsync(chore, _home.Project, (_projectBudget, "5"));
        await CreateAsync(chore, _home.Task, (_taskHours, "1"));

        var totals = await _rollUps.GetAsync(chore.Id);

        Assert.That(totals.Select(t => t.Field.Id), Is.EqualTo(new[] { _taskHours.Id, _projectBudget.Id }));
    }

    [Test]
    public async Task GetAsync_IgnoresTrashedDescendants_AndWhatsUnderThem()
    {
        var house = await CreateAsync(null, _home.Project);
        await CreateAsync(house, _home.Project, (_projectBudget, "10"));
        var trashed = await CreateAsync(house, _home.Project, (_projectBudget, "20"));
        await CreateAsync(trashed, _home.Project, (_projectBudget, "40"));

        await _trash.MoveToTrashAsync(trashed.Id, cascade: true);

        Assert.That((await _rollUps.GetAsync(house.Id)).Single().Total, Is.EqualTo(10m));
    }

    [Test]
    public async Task GetAsync_DoesntCountTheItemsOwnValue()
    {
        var house = await CreateAsync(null, _home.Project, (_projectBudget, "1000"));
        await CreateAsync(house, _home.Project, (_projectBudget, "1"));

        Assert.That((await _rollUps.GetAsync(house.Id)).Single().Total, Is.EqualTo(1m));
    }

    [Test]
    public async Task GetAsync_LeavesOutFieldsThatDontRollUp()
    {
        var effort = AddField(_home.Task, "Effort", CustomFieldKind.Number, rollsUp: false);
        var chore = await CreateAsync(null, _home.Task);
        await CreateAsync(chore, _home.Task, (effort, "8"));

        Assert.That(await _rollUps.GetAsync(chore.Id), Is.Empty);
    }

    [Test]
    public async Task GetAsync_ForAnItemWithoutChildren_IsEmpty()
    {
        var chore = await CreateAsync(null, _home.Task, (_taskHours, "2"));

        Assert.That(await _rollUps.GetAsync(chore.Id), Is.Empty);
    }

    [Test]
    public async Task GetAsync_ForAMissingOrTrashedItem_Throws()
    {
        var chore = await CreateAsync(null, _home.Task);
        await _trash.MoveToTrashAsync(chore.Id);

        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<EntityNotFoundException>(() => _rollUps.GetAsync(Guid.NewGuid()));
            Assert.ThrowsAsync<EntityNotFoundException>(() => _rollUps.GetAsync(chore.Id));
        });
    }

    private async Task<WorkItem> CreateAsync(
        WorkItem? parent,
        Guid typeId,
        params (CustomFieldDefinition Field, string Json)[] values)
    {
        var item = await _workItems.CreateAsync(
            "Item", null, parent?.Id, _home.ToDo, typeId: typeId, spaceId: parent is null ? _home.Id : null);
        if (values.Length == 0)
        {
            return item;
        }

        var written = await _workItems.SetCustomFieldsAsync(
            item.Id, values.ToDictionary(v => v.Field.Id, v => JsonSerializer.Deserialize<JsonElement>(v.Json)));
        return written.Item;
    }

    private CustomFieldDefinition AddField(Guid typeId, string name, CustomFieldKind kind, bool rollsUp)
    {
        var order = _db.CustomFields.Count(f => f.TypeId == typeId);
        var field = CustomFieldDefinition.Create(typeId, name, kind, order, rollsUp: rollsUp);
        _db.CustomFields.Add(field);
        _db.SaveChanges();
        return field;
    }
}
