using System.Text.Json;
using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

/// <summary>Moving a work item, and the subtree under it, into another Space (ADR-0001).</summary>
[TestFixture]
public class WorkItemSpaceMoveTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid Village = Guid.NewGuid();
    private static readonly Status HomeToDo = TestStatuses.ToDo(Home);
    private static readonly Status VillageBooked = Status.Create(Village, "Booked", 0, StatusCategory.Doing, "#FB8C00");
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly Guid HomeRoom = Guid.NewGuid();
    private static readonly Guid VillageChore = Guid.NewGuid();
    private static readonly Guid HomeHigh = Guid.NewGuid();
    private static readonly Guid VillageUrgent = Guid.NewGuid();

    private static readonly SpaceMoveValues InVillage = new(VillageBooked, VillageChore, VillageUrgent);

    private static WorkItem NewHomeItem() =>
        WorkItem.Create(Home, "Kitchen", null, HomeToDo, rank: 1.0, Today, typeId: HomeRoom, priorityId: HomeHigh);

    private static WorkItem NewVillageItem() => WorkItem.Create(Village, "Fence", null, VillageBooked, rank: 1.0, Today);

    private static IEnumerable<string> Fields(IEnumerable<FieldChange> changes) => changes.Select(c => c.Field);

    [Test]
    public void MoveToSpace_TakesTheTargetSpaceAndItsValues()
    {
        var item = NewHomeItem();

        item.MoveToSpace(Village, parent: null, rank: 3.0, InVillage, Today);

        Assert.Multiple(() =>
        {
            Assert.That(item.SpaceId, Is.EqualTo(Village));
            Assert.That(item.StatusId, Is.EqualTo(VillageBooked.Id));
            Assert.That(item.TypeId, Is.EqualTo(VillageChore));
            Assert.That(item.PriorityId, Is.EqualTo(VillageUrgent));
            Assert.That(item.Rank, Is.EqualTo(3.0));
        });
    }

    [Test]
    public void MoveToSpace_ReportsEveryChangeWithItsOldValue()
    {
        var item = NewHomeItem();

        var changes = item.MoveToSpace(Village, parent: null, rank: 3.0, InVillage, Today);

        Assert.That(
            changes.Select(c => (c.Field, c.OldValue, c.NewValue)),
            Is.SupersetOf(new (string, object?, object?)[]
            {
                (WorkItemField.SpaceId, Home, Village),
                (WorkItemField.StatusId, HomeToDo.Id, VillageBooked.Id),
                (WorkItemField.TypeId, HomeRoom, VillageChore),
                (WorkItemField.PriorityId, HomeHigh, VillageUrgent),
            }));
    }

    [Test]
    public void MoveToSpace_UnderAParentThere_TakesThatParent()
    {
        var item = NewHomeItem();
        var parent = NewVillageItem();

        var changes = item.MoveToSpace(Village, parent, rank: 1.0, InVillage, Today);

        Assert.Multiple(() =>
        {
            Assert.That(item.ParentId, Is.EqualTo(parent.Id));
            Assert.That(Fields(changes), Does.Contain(WorkItemField.ParentId));
        });
    }

    [Test]
    public void MoveToSpace_ClearsTheCustomFieldValues_AsAChangeOfTypeDoes()
    {
        var item = NewHomeItem();
        var budget = CustomFieldDefinition.Create(HomeRoom, "Budget", CustomFieldKind.Number, order: 0);
        item.SetCustomFields(CustomFieldPatch.Validate(
            HomeRoom, [budget], new Dictionary<Guid, JsonElement> { [budget.Id] = JsonSerializer.Deserialize<JsonElement>("12") }));

        var changes = item.MoveToSpace(Village, parent: null, rank: 1.0, InVillage, Today);

        Assert.Multiple(() =>
        {
            Assert.That(item.CustomFields, Is.Empty);
            Assert.That(Fields(changes), Does.Contain(WorkItemField.CustomField(budget.Id)));
        });
    }

    [Test]
    public void MoveToSpace_IntoItsOwnSpace_IsRejected()
    {
        var item = NewHomeItem();

        Assert.Throws<DomainValidationException>(() =>
            item.MoveToSpace(Home, parent: null, rank: 1.0, new SpaceMoveValues(HomeToDo, null, null), Today));
        Assert.That(item.StatusId, Is.EqualTo(HomeToDo.Id));
    }

    [Test]
    public void MoveToSpace_UnderAParentInAThirdSpace_IsRejectedAndChangesNothing()
    {
        var item = NewHomeItem();
        var elsewhere = WorkItem.Create(Guid.NewGuid(), "Elsewhere", null, TestStatuses.ToDo(Guid.NewGuid()), rank: 1.0, Today);

        Assert.Throws<CrossSpaceParentException>(() => item.MoveToSpace(Village, elsewhere, rank: 1.0, InVillage, Today));
        Assert.Multiple(() =>
        {
            Assert.That(item.SpaceId, Is.EqualTo(Home));
            Assert.That(item.ParentId, Is.Null);
        });
    }

    [Test]
    public void FollowParentToSpace_TakesTheParentsNewSpace_AndKeepsItsParentAndRank()
    {
        var parent = NewHomeItem();
        var child = WorkItem.CreateChildOf(parent, "Paint", null, HomeToDo, rank: 7.0, Today);
        parent.MoveToSpace(Village, parent: null, rank: 1.0, InVillage, Today);

        var changes = child.FollowParentToSpace(parent, InVillage, Today);

        Assert.Multiple(() =>
        {
            Assert.That(child.SpaceId, Is.EqualTo(Village));
            Assert.That(child.ParentId, Is.EqualTo(parent.Id));
            Assert.That(child.Rank, Is.EqualTo(7.0));
            Assert.That(child.StatusId, Is.EqualTo(VillageBooked.Id));
            Assert.That(Fields(changes), Does.Contain(WorkItemField.SpaceId).And.Not.Contain(WorkItemField.ParentId));
        });
    }

    [Test]
    public void FollowParentToSpace_ForAnItemThatIsntItsChild_IsRejected()
    {
        var item = NewHomeItem();
        var stranger = NewVillageItem();

        Assert.Throws<DomainValidationException>(() => item.FollowParentToSpace(stranger, InVillage, Today));
        Assert.That(item.SpaceId, Is.EqualTo(Home));
    }

    [Test]
    public void FollowParentToSpace_BeforeTheParentHasMoved_IsRejected()
    {
        var parent = NewHomeItem();
        var child = WorkItem.CreateChildOf(parent, "Paint", null, HomeToDo, rank: 1.0, Today);

        Assert.Throws<DomainValidationException>(() => child.FollowParentToSpace(parent, InVillage, Today));
    }
}
