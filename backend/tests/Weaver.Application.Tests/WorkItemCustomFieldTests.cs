using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>Setting custom field values through <see cref="IWorkItemService.SetCustomFieldsAsync"/>.</summary>
[TestFixture]
public class WorkItemCustomFieldTests
{
    private WeaverDbContext _db = null!;
    private WorkItemService _workItems = null!;
    private WorkItemHistoryService _history = null!;
    private User _alice = null!;
    private TestSpace _home = null!;
    private CustomFieldDefinition _budget = null!;
    private CustomFieldDefinition _owner = null!;
    private CustomFieldDefinition _floor = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
        _alice = TestUsers.Add(_db, "alice");
        _home = TestSpaces.Add(_db);
        _budget = AddField(_home.Project, "Budget", CustomFieldKind.Currency);
        _owner = AddField(_home.Project, "Owner", CustomFieldKind.User);
        _floor = AddField(_home.Project, "Floor", CustomFieldKind.SingleSelect, "Oak", "Tiles");
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), new FakeCallerContext(_alice.Id));
        _history = new WorkItemHistoryService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task SetCustomFieldsAsync_StoresTheValue_AndReportsItAsChanged()
    {
        var item = await CreateProjectAsync();

        var result = await _workItems.SetCustomFieldsAsync(item.Id, Values((_budget, "1200")));

        Assert.Multiple(async () =>
        {
            Assert.That(result.ChangedFields, Is.EqualTo(new[] { $"fields.{_budget.Id}" }));
            Assert.That((await ReloadAsync(item)).CustomFields[_budget.Id].GetRawText(), Is.EqualTo("1200"));
        });
    }

    [Test]
    public async Task SetCustomFieldsAsync_RecordsTheOldAndNewValueInHistory_UnderTheFieldsName()
    {
        var item = await CreateProjectAsync();
        await _workItems.SetCustomFieldsAsync(item.Id, Values((_budget, "1200")));

        await _workItems.SetCustomFieldsAsync(item.Id, Values((_budget, "900")));

        var entry = (await _history.ListForWorkItemAsync(item.Id)).First();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Field, Is.EqualTo($"fields.{_budget.Id}"));
            Assert.That(entry.OldValue?.GetRawText(), Is.EqualTo("1200"));
            Assert.That(entry.NewValue?.GetRawText(), Is.EqualTo("900"));
            Assert.That(entry.AccountId, Is.EqualTo(_alice.Id));
        });
    }

    [Test]
    public async Task History_NamesTheFieldAndTheOptionOrUserAValueRefersTo()
    {
        var item = await CreateProjectAsync();

        await _workItems.SetCustomFieldsAsync(
            item.Id, Values((_owner, $"\"{_alice.Id}\""), (_floor, $"\"{_floor.Options[1].Id}\"")));

        var entries = await _history.ListForWorkItemAsync(item.Id);
        var owner = entries.Single(e => e.Field == _owner.ChangeName);
        var floor = entries.Single(e => e.Field == _floor.ChangeName);
        Assert.Multiple(() =>
        {
            Assert.That(owner.FieldName, Is.EqualTo("Owner"));
            Assert.That(owner.NewName, Is.EqualTo("alice"));
            Assert.That(floor.FieldName, Is.EqualTo("Floor"));
            Assert.That(floor.NewName, Is.EqualTo("Tiles"));
        });
    }

    [Test]
    public async Task SetCustomFieldsAsync_WithAStaleExpectedVersion_IsRejected()
    {
        var item = await CreateProjectAsync();

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() =>
            _workItems.SetCustomFieldsAsync(item.Id, Values((_budget, "1")), expectedVersion: item.Version + 1));
    }

    [Test]
    public async Task SetCustomFieldsAsync_WithAValueOfTheWrongKind_IsRejected()
    {
        var item = await CreateProjectAsync();

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _workItems.SetCustomFieldsAsync(item.Id, Values((_budget, "\"a lot\""))));
    }

    [Test]
    public async Task SetCustomFieldsAsync_WithAnUnknownOption_IsRejected()
    {
        var item = await CreateProjectAsync();

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _workItems.SetCustomFieldsAsync(item.Id, Values((_floor, $"\"{Guid.NewGuid()}\""))));
    }

    [Test]
    public async Task SetCustomFieldsAsync_WithAnUnknownUser_IsRejected()
    {
        var item = await CreateProjectAsync();

        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _workItems.SetCustomFieldsAsync(item.Id, Values((_owner, $"\"{Guid.NewGuid()}\""))));
    }

    [Test]
    public async Task SetCustomFieldsAsync_WithAnotherTypesField_IsRejected()
    {
        var chores = AddField(_home.Task, "Effort", CustomFieldKind.Number);
        var item = await CreateProjectAsync();

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _workItems.SetCustomFieldsAsync(item.Id, Values((chores, "3"))));
    }

    [Test]
    public async Task ChangingTheType_ClearsTheValues_AndRecordsWhatWasCleared()
    {
        var item = await CreateProjectAsync();
        await _workItems.SetCustomFieldsAsync(item.Id, Values((_budget, "1200")));

        var result = await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { TypeId = _home.Task });

        var cleared = (await _history.ListForWorkItemAsync(item.Id)).First(e => e.Field == _budget.ChangeName);
        Assert.Multiple(async () =>
        {
            Assert.That(result.ChangedFields, Is.EquivalentTo(new[] { WorkItemField.TypeId, _budget.ChangeName }));
            Assert.That((await ReloadAsync(item)).CustomFields, Is.Empty);
            Assert.That(cleared.OldValue?.GetRawText(), Is.EqualTo("1200"));
            Assert.That(cleared.NewValue, Is.Null);
        });
    }

    private static IReadOnlyDictionary<Guid, JsonElement> Values(params (CustomFieldDefinition Field, string Json)[] values) =>
        values.ToDictionary(v => v.Field.Id, v => JsonSerializer.Deserialize<JsonElement>(v.Json));

    private CustomFieldDefinition AddField(Guid typeId, string name, CustomFieldKind kind, params string[] options)
    {
        var order = _db.CustomFields.Count(f => f.TypeId == typeId);
        var field = CustomFieldDefinition.Create(typeId, name, kind, order, options);
        _db.CustomFields.Add(field);
        _db.SaveChanges();
        return field;
    }

    private Task<WorkItem> CreateProjectAsync() =>
        _workItems.CreateAsync("Kitchen", null, null, _home.ToDo, typeId: _home.Project, spaceId: _home.Id);

    private Task<WorkItem> ReloadAsync(WorkItem item) =>
        _db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item.Id);
}
