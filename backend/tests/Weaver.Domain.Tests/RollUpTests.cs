using System.Text.Json;
using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class RollUpTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid ToDo = Guid.NewGuid();
    private static readonly Guid Room = Guid.NewGuid();
    private static readonly Guid Chore = Guid.NewGuid();

    private static CustomFieldDefinition Field(Guid typeId, string name, CustomFieldKind kind, bool rollsUp = true) =>
        CustomFieldDefinition.Create(typeId, name, kind, order: 0, rollsUp: rollsUp);

    private static WorkItem Item(Guid typeId, params (CustomFieldDefinition Field, string Json)[] values)
    {
        var item = WorkItem.Create(Home, "Item", null, ToDo, rank: 1.0, typeId: typeId);
        var fields = values.Select(v => v.Field).ToList();
        item.SetCustomFields(CustomFieldPatch.Validate(
            typeId, fields, values.ToDictionary(v => v.Field.Id, v => JsonSerializer.Deserialize<JsonElement>(v.Json))));
        return item;
    }

    [TestCase(CustomFieldKind.Number)]
    [TestCase(CustomFieldKind.Currency)]
    public void ANumberOrCurrencyField_CanRollUp(CustomFieldKind kind)
    {
        Assert.That(Field(Room, "Amount", kind).RollsUp, Is.True);
    }

    [TestCase(CustomFieldKind.Text)]
    [TestCase(CustomFieldKind.Checkbox)]
    [TestCase(CustomFieldKind.Date)]
    public void AnyOtherKind_CantRollUp(CustomFieldKind kind)
    {
        Assert.Throws<DomainValidationException>(() => Field(Room, "Other", kind));
    }

    [Test]
    public void RollUp_CanBeTurnedOnAndOffLater()
    {
        var hours = Field(Room, "Hours", CustomFieldKind.Number, rollsUp: false);

        hours.SetRollsUp(true);
        var on = hours.RollsUp;
        hours.SetRollsUp(false);

        Assert.Multiple(() =>
        {
            Assert.That(on, Is.True);
            Assert.That(hours.RollsUp, Is.False);
        });
    }

    [Test]
    public void Sum_AddsUpEveryDescendantsValue_PerField()
    {
        var budget = Field(Room, "Budget", CustomFieldKind.Currency);
        var hours = Field(Room, "Hours", CustomFieldKind.Number);

        var totals = RollUp.Sum(
            [Item(Room, (budget, "100.25"), (hours, "2")), Item(Room, (budget, "50")), Item(Room, (hours, "1.5"))],
            [budget, hours]);

        Assert.That(totals.Select(t => (t.Field, t.Total)), Is.EqualTo(new[] { (budget, 150.25m), (hours, 3.5m) }));
    }

    [Test]
    public void Sum_CoversDescendantsOfAnyTypeThatHasTheField()
    {
        var budget = Field(Room, "Budget", CustomFieldKind.Currency);
        var effort = Field(Chore, "Effort", CustomFieldKind.Number);

        var totals = RollUp.Sum([Item(Room, (budget, "10")), Item(Chore, (effort, "3")), Item(Chore)], [budget, effort]);

        Assert.That(totals.Select(t => (t.Field, t.Total)), Is.EqualTo(new[] { (budget, 10m), (effort, 3m) }));
    }

    [Test]
    public void Sum_LeavesOutAFieldThatDoesntRollUp()
    {
        var budget = Field(Room, "Budget", CustomFieldKind.Currency, rollsUp: false);

        Assert.That(RollUp.Sum([Item(Room, (budget, "10"))], [budget]), Is.Empty);
    }

    [Test]
    public void Sum_LeavesOutAFieldNoDescendantHasAValueFor()
    {
        var budget = Field(Room, "Budget", CustomFieldKind.Currency);

        Assert.That(RollUp.Sum([Item(Room)], [budget]), Is.Empty);
    }

    /// <summary>
    /// A currency field holds amounts in one currency (ADR-0002), so a roll-up only ever adds up
    /// one field's values: two currency fields are never summed together, even under one name.
    /// </summary>
    [Test]
    public void Sum_NeverAddsTwoCurrencyFieldsTogether_EvenWithTheSameName()
    {
        var roomBudget = Field(Room, "Budget", CustomFieldKind.Currency);
        var choreBudget = Field(Chore, "Budget", CustomFieldKind.Currency);

        var totals = RollUp.Sum(
            [Item(Room, (roomBudget, "100")), Item(Chore, (choreBudget, "7"))], [roomBudget, choreBudget]);

        Assert.That(totals.Select(t => (t.Field, t.Total)), Is.EqualTo(new[] { (roomBudget, 100m), (choreBudget, 7m) }));
    }
}
