using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class CustomFieldServiceTests
{
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private CustomFieldService _fields = null!;
    private WorkItemService _workItems = null!;
    private WorkItemHistoryService _history = null!;
    private User _admin = null!;
    private TestSpace _home = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _home = TestSpaces.Add(_db);
        _caller = new FakeCallerContext(_admin.Id);
        _fields = new CustomFieldService(_db, new AdminAuthorizer(_db), _caller);
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), _caller);
        _history = new WorkItemHistoryService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task CreateAsync_AppendsTheFieldToItsTypesFields()
    {
        await _fields.CreateAsync(_admin.Id, _home.Project, "Budget", CustomFieldKind.Currency, []);
        await _fields.CreateAsync(_admin.Id, _home.Project, "Floor", CustomFieldKind.SingleSelect, ["Oak", "Tiles"]);

        var fields = await _fields.GetAllAsync(_home.Project);

        Assert.Multiple(() =>
        {
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(new[] { "Budget", "Floor" }));
            Assert.That(fields[1].Options.Select(o => o.Name), Is.EqualTo(new[] { "Oak", "Tiles" }));
        });
    }

    [Test]
    public async Task GetAllAsync_OnlyListsThatTypesFields()
    {
        await _fields.CreateAsync(_admin.Id, _home.Project, "Budget", CustomFieldKind.Currency, []);

        Assert.That(await _fields.GetAllAsync(_home.Task), Is.Empty);
    }

    [Test]
    public async Task GetAllAsync_ForSeveralTypes_ListsEachTypesFieldsInOrder()
    {
        await _fields.CreateAsync(_admin.Id, _home.Project, "Budget", CustomFieldKind.Currency, []);
        await _fields.CreateAsync(_admin.Id, _home.Task, "Effort", CustomFieldKind.Number, []);
        await _fields.CreateAsync(_admin.Id, _home.Project, "Due", CustomFieldKind.Date, []);
        await _fields.CreateAsync(_admin.Id, _home.Goal, "Why", CustomFieldKind.Markdown, []);

        var fields = await _fields.GetAllAsync([_home.Project, _home.Task]);

        Assert.That(
            fields.GroupBy(f => f.TypeId).ToDictionary(g => g.Key, g => g.Select(f => f.Name).ToList()),
            Is.EquivalentTo(new Dictionary<Guid, List<string>>
            {
                [_home.Project] = ["Budget", "Due"],
                [_home.Task] = ["Effort"],
            }));
    }

    [Test]
    public void CreateAsync_OnAnUnknownType_IsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _fields.CreateAsync(_admin.Id, Guid.NewGuid(), "Budget", CustomFieldKind.Currency, []));
    }

    [Test]
    public async Task CreateAsync_WithANameTheTypeAlreadyHas_IsRejected()
    {
        await _fields.CreateAsync(_admin.Id, _home.Project, "Budget", CustomFieldKind.Currency, []);

        Assert.ThrowsAsync<LookupNameTakenException>(() =>
            _fields.CreateAsync(_admin.Id, _home.Project, "budget", CustomFieldKind.Number, []));
    }

    [Test]
    public void CreateAsync_ByANonAdmin_IsForbidden()
    {
        var member = TestUsers.Add(_db, "member");

        Assert.ThrowsAsync<AdminRequiredException>(() =>
            _fields.CreateAsync(member.Id, _home.Project, "Budget", CustomFieldKind.Currency, []));
    }

    [Test]
    public void CreateAsync_ByAnAgent_IsRefused()
    {
        var agent = TestUsers.AddAgentFlaggedAsAdmin(_db, "Nightly triage");

        Assert.ThrowsAsync<AgentAdministrationException>(() =>
            _fields.CreateAsync(agent.Id, _home.Project, "Budget", CustomFieldKind.Currency, []));
    }

    [Test]
    public async Task UpdateAsync_RenamesTheFieldAndRevisesItsOptions()
    {
        var floor = await _fields.CreateAsync(_admin.Id, _home.Project, "Floor", CustomFieldKind.SingleSelect, ["Oak", "Tiles"]);
        var tiles = floor.Options[1].Id;

        await _fields.UpdateAsync(_admin.Id, floor.Id, "Flooring", [new(tiles, "Stone tiles"), new(null, "Pine")], rollsUp: false);

        var updated = (await _fields.GetAllAsync(_home.Project)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(updated.Name, Is.EqualTo("Flooring"));
            Assert.That(updated.Options.Select(o => o.Name), Is.EqualTo(new[] { "Stone tiles", "Pine" }));
            Assert.That(updated.Options[0].Id, Is.EqualTo(tiles));
        });
    }

    [Test]
    public async Task UpdateAsync_RemovingAnOption_ClearsItFromItems_AndRecordsIt()
    {
        var floor = await _fields.CreateAsync(_admin.Id, _home.Project, "Floor", CustomFieldKind.SingleSelect, ["Oak", "Tiles"]);
        var (oak, tiles) = (floor.Options[0].Id, floor.Options[1].Id);
        var oakRoom = await CreateProjectAsync();
        var tiledRoom = await CreateProjectAsync();
        await SetAsync(oakRoom, floor, $"\"{oak}\"");
        await SetAsync(tiledRoom, floor, $"\"{tiles}\"");

        await _fields.UpdateAsync(_admin.Id, floor.Id, "Floor", [new(tiles, "Tiles")], rollsUp: false);

        var cleared = (await _history.ListForWorkItemAsync(oakRoom.Id)).First();
        Assert.Multiple(async () =>
        {
            Assert.That((await ReloadAsync(oakRoom)).CustomFields, Is.Empty);
            Assert.That((await ReloadAsync(tiledRoom)).CustomFields[floor.Id].GetString(), Is.EqualTo(tiles.ToString()));
            Assert.That(cleared.Field, Is.EqualTo(floor.ChangeName));
            Assert.That(cleared.NewValue, Is.Null);
            Assert.That(cleared.AccountId, Is.EqualTo(_admin.Id));
        });
    }

    [Test]
    public async Task CreateAndUpdate_SetWhetherTheFieldRollsUp()
    {
        var hours = await _fields.CreateAsync(_admin.Id, _home.Task, "Hours", CustomFieldKind.Number, [], rollsUp: true);
        var created = (await _fields.GetAllAsync(_home.Task)).Single().RollsUp;

        await _fields.UpdateAsync(_admin.Id, hours.Id, "Hours", [], rollsUp: false);

        Assert.Multiple(async () =>
        {
            Assert.That(created, Is.True);
            Assert.That((await _fields.GetAllAsync(_home.Task)).Single().RollsUp, Is.False);
        });
    }

    [Test]
    public async Task UpdateAsync_TurningOnRollUpForAFieldThatIsntANumber_IsRejected()
    {
        var notes = await _fields.CreateAsync(_admin.Id, _home.Task, "Notes", CustomFieldKind.Text, []);

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _fields.UpdateAsync(_admin.Id, notes.Id, "Notes", [], rollsUp: true));
    }

    [Test]
    public async Task GetAllInSpaceAsync_ListsEveryTypesFieldsInThatSpaceOnly()
    {
        var garden = TestSpaces.Add(_db, "Garden");
        await _fields.CreateAsync(_admin.Id, _home.Project, "Budget", CustomFieldKind.Currency, []);
        await _fields.CreateAsync(_admin.Id, _home.Task, "Hours", CustomFieldKind.Number, []);
        await _fields.CreateAsync(_admin.Id, garden.Task, "Seeds", CustomFieldKind.Number, []);

        var fields = await _fields.GetAllInSpaceAsync(_home.Id);

        Assert.That(fields.Select(f => f.Name), Is.EquivalentTo(new[] { "Budget", "Hours" }));
    }

    [Test]
    public async Task ReorderAsync_ChangesTheOrderOfTheTypesFields()
    {
        var budget = await _fields.CreateAsync(_admin.Id, _home.Project, "Budget", CustomFieldKind.Currency, []);
        var due = await _fields.CreateAsync(_admin.Id, _home.Project, "Due", CustomFieldKind.Date, []);

        await _fields.ReorderAsync(_admin.Id, _home.Project, [due.Id, budget.Id]);

        Assert.That((await _fields.GetAllAsync(_home.Project)).Select(f => f.Name), Is.EqualTo(new[] { "Due", "Budget" }));
    }

    [Test]
    public async Task DeleteAsync_RemovesTheFieldAndItsValues_RecordingEachInHistory()
    {
        var budget = await _fields.CreateAsync(_admin.Id, _home.Project, "Budget", CustomFieldKind.Currency, []);
        var item = await CreateProjectAsync();
        await SetAsync(item, budget, "1200");

        await _fields.DeleteAsync(_admin.Id, budget.Id);

        var removed = (await _history.ListForWorkItemAsync(item.Id)).First();
        Assert.Multiple(async () =>
        {
            Assert.That(await _fields.GetAllAsync(_home.Project), Is.Empty);
            Assert.That((await ReloadAsync(item)).CustomFields, Is.Empty);
            Assert.That(removed.Field, Is.EqualTo(budget.ChangeName));
            Assert.That(removed.OldValue?.GetRawText(), Is.EqualTo("1200"));
            Assert.That(removed.NewValue, Is.Null);
        });
    }

    [Test]
    public void DeleteAsync_ByANonAdmin_IsForbidden()
    {
        var member = TestUsers.Add(_db, "member");

        Assert.ThrowsAsync<AdminRequiredException>(() => _fields.DeleteAsync(member.Id, Guid.NewGuid()));
    }

    private Task<WorkItem> CreateProjectAsync() =>
        _workItems.CreateAsync("Kitchen", null, null, _home.ToDo, typeId: _home.Project, spaceId: _home.Id);

    private Task<WorkItemWriteResult> SetAsync(WorkItem item, CustomFieldDefinition field, string json) =>
        _workItems.SetCustomFieldsAsync(
            item.Id, new Dictionary<Guid, JsonElement> { [field.Id] = JsonSerializer.Deserialize<JsonElement>(json) });

    private Task<WorkItem> ReloadAsync(WorkItem item) =>
        _db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item.Id);
}
