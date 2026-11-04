using System.Text.Json;
using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class WorkItemCustomFieldTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid ToDo = Guid.NewGuid();
    private static readonly Guid Room = Guid.NewGuid();
    private static readonly Guid Chore = Guid.NewGuid();

    private static readonly CustomFieldDefinition Budget =
        CustomFieldDefinition.Create(Room, "Budget", CustomFieldKind.Currency, order: 0);

    private static readonly CustomFieldDefinition Floor =
        CustomFieldDefinition.Create(Room, "Floor", CustomFieldKind.SingleSelect, order: 1, ["Oak", "Tiles"]);

    private static WorkItem NewRoom() => WorkItem.Create(Home, "Kitchen", null, ToDo, rank: 1.0, typeId: Room);

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static CustomFieldPatch Patch(Guid? typeId, params (CustomFieldDefinition Field, string Json)[] values) =>
        CustomFieldPatch.Validate(typeId, [Budget, Floor], values.ToDictionary(v => v.Field.Id, v => Json(v.Json)));

    private static string? ValueOf(WorkItem item, CustomFieldDefinition field) =>
        item.CustomFields.TryGetValue(field.Id, out var value) ? value.GetRawText() : null;

    [Test]
    public void SetCustomFields_StoresTheNormalizedValue_AndReportsTheChange()
    {
        var item = NewRoom();

        var changes = item.SetCustomFields(Patch(Room, (Budget, "1200.50")));

        Assert.Multiple(() =>
        {
            Assert.That(ValueOf(item, Budget), Is.EqualTo("1200.5"));
            Assert.That(changes.Select(c => c.Field), Is.EqualTo(new[] { $"fields.{Budget.Id}" }));
            Assert.That(changes[0].OldValue, Is.Null);
        });
    }

    [Test]
    public void SetCustomFields_LeavesFieldsNotInThePatchAlone()
    {
        var item = NewRoom();
        var oak = Floor.Options[0].Id;
        item.SetCustomFields(Patch(Room, (Floor, $"\"{oak}\"")));

        item.SetCustomFields(Patch(Room, (Budget, "10")));

        Assert.That(ValueOf(item, Floor), Is.EqualTo($"\"{oak}\""));
    }

    [Test]
    public void SetCustomFields_ToNull_ClearsTheValue()
    {
        var item = NewRoom();
        item.SetCustomFields(Patch(Room, (Budget, "10")));

        var changes = item.SetCustomFields(Patch(Room, (Budget, "null")));

        Assert.Multiple(() =>
        {
            Assert.That(item.CustomFields, Does.Not.ContainKey(Budget.Id));
            Assert.That(((JsonElement)changes.Single().OldValue!).GetRawText(), Is.EqualTo("10"));
            Assert.That(changes.Single().NewValue, Is.Null);
        });
    }

    [Test]
    public void SetCustomFields_ToTheValueItAlreadyHas_IsNotAChange()
    {
        var item = NewRoom();
        item.SetCustomFields(Patch(Room, (Budget, "10.0")));

        Assert.That(item.SetCustomFields(Patch(Room, (Budget, "10"))), Is.Empty);
    }

    [Test]
    public void Validate_AFieldTheTypeDoesNotHave_IsRejected()
    {
        var other = CustomFieldDefinition.Create(Chore, "Effort", CustomFieldKind.Number, order: 0);

        Assert.Throws<DomainValidationException>(() =>
            CustomFieldPatch.Validate(Room, [Budget], new Dictionary<Guid, JsonElement> { [other.Id] = Json("3") }));
    }

    [Test]
    public void Validate_OnAnItemWithoutAType_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() =>
            CustomFieldPatch.Validate(null, [], new Dictionary<Guid, JsonElement> { [Budget.Id] = Json("3") }));
    }

    [Test]
    public void Validate_ListsTheUsersItNames()
    {
        var owner = CustomFieldDefinition.Create(Room, "Owner", CustomFieldKind.User, order: 2);
        var riley = Guid.NewGuid();

        var patch = CustomFieldPatch.Validate(Room, [owner], new Dictionary<Guid, JsonElement> { [owner.Id] = Json($"\"{riley}\"") });

        Assert.That(patch.UserIds, Is.EqualTo(new[] { riley }));
    }

    [Test]
    public void SetCustomFields_ValidatedForAnotherType_IsRejected()
    {
        var item = NewRoom();
        var patch = Patch(Room, (Budget, "10"));
        item.ChangeType(Chore);

        Assert.Throws<DomainValidationException>(() => item.SetCustomFields(patch));
    }

    [Test]
    public void ChangingTheType_ClearsTheOldTypesValues_AndReportsEach()
    {
        var item = NewRoom();
        item.SetCustomFields(Patch(Room, (Budget, "10"), (Floor, $"\"{Floor.Options[1].Id}\"")));

        var changes = item.UpdateDetails(new WorkItemDetailsPatch { TypeId = Chore });

        Assert.Multiple(() =>
        {
            Assert.That(item.CustomFields, Is.Empty);
            Assert.That(changes.Select(c => c.Field), Is.EquivalentTo(new[]
            {
                WorkItemField.TypeId, $"fields.{Budget.Id}", $"fields.{Floor.Id}",
            }));
            Assert.That(changes.Where(c => c.Field.StartsWith("fields.")).Select(c => c.NewValue), Is.All.Null);
        });
    }

    [Test]
    public void ChangeType_ByAnAdminDeletingTheType_ClearsTheValuesToo()
    {
        var item = NewRoom();
        item.SetCustomFields(Patch(Room, (Budget, "10")));

        var changes = item.ChangeType(null);

        Assert.Multiple(() =>
        {
            Assert.That(item.CustomFields, Is.Empty);
            Assert.That(changes.Select(c => c.Field), Is.EqualTo(new[] { WorkItemField.TypeId, $"fields.{Budget.Id}" }));
        });
    }

    [Test]
    public void KeepingTheSameType_KeepsTheValues()
    {
        var item = NewRoom();
        item.SetCustomFields(Patch(Room, (Budget, "10")));

        item.UpdateDetails(new WorkItemDetailsPatch { TypeId = Room, Title = "Big kitchen" });

        Assert.That(ValueOf(item, Budget), Is.EqualTo("10"));
    }

    [Test]
    public void RemoveCustomField_DropsItsValue_AndReportsIt()
    {
        var item = NewRoom();
        item.SetCustomFields(Patch(Room, (Budget, "10")));

        var changes = item.RemoveCustomField(Budget.Id);

        Assert.Multiple(() =>
        {
            Assert.That(item.CustomFields, Is.Empty);
            Assert.That(changes.Single().Field, Is.EqualTo($"fields.{Budget.Id}"));
        });
    }

    [Test]
    public void RemoveCustomField_ThatHasNoValue_IsNotAChange()
    {
        Assert.That(NewRoom().RemoveCustomField(Budget.Id), Is.Empty);
    }

    [Test]
    public void PruneCustomField_ClearsAChoiceWhoseOptionWasRemoved()
    {
        var floor = CustomFieldDefinition.Create(Room, "Floor", CustomFieldKind.SingleSelect, order: 1, ["Oak", "Tiles"]);
        var item = NewRoom();
        item.SetCustomFields(CustomFieldPatch.Validate(
            Room, [floor], new Dictionary<Guid, JsonElement> { [floor.Id] = Json($"\"{floor.Options[0].Id}\"") }));
        floor.ReviseOptions([new(floor.Options[1].Id, "Tiles")]);

        var changes = item.PruneCustomField(floor);

        Assert.Multiple(() =>
        {
            Assert.That(item.CustomFields, Is.Empty);
            Assert.That(changes.Single().Field, Is.EqualTo($"fields.{floor.Id}"));
        });
    }

    [Test]
    public void CopyForOccurrence_CopiesTheValues()
    {
        var item = NewRoom();
        item.SetCustomFields(Patch(Room, (Budget, "10")));

        var copy = item.CopyForOccurrence(null, ToDo, 1.0, new DateOnly(2026, 12, 1));

        Assert.That(ValueOf(copy, Budget), Is.EqualTo("10"));
    }
}
