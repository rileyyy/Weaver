using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemDtoMapperTests
{
    [Test]
    public async Task ToDtoAsync_AWhenWithALiveBlocker_IsResolved()
    {
        var item = ItemWithWhen();
        var mapper = Mapper(new FakeWorkItemBlockers(item.Id));

        var dto = await mapper.ToDtoAsync(item);

        Assert.That(dto.WhenResolved, Is.True);
    }

    [Test]
    public async Task ToDtoAsync_AWhenWithNoDatesOrBlocker_IsUnresolved()
    {
        var item = ItemWithWhen();
        var mapper = Mapper(new FakeWorkItemBlockers());

        var dto = await mapper.ToDtoAsync(item);

        Assert.That(dto.WhenResolved, Is.False);
    }

    [Test]
    public async Task ToDtosAsync_AsksAboutBlockersOnce_AndOnlyForItemsWhoseWhenDependsOnThem()
    {
        var undated = ItemWithWhen();
        var dated = ItemWithWhen();
        dated.Reschedule(new DateOnly(2026, 11, 9), null);
        var noWhen = NewItem();
        var blockers = new FakeWorkItemBlockers(undated.Id, dated.Id, noWhen.Id);
        var mapper = Mapper(blockers);

        var dtos = await mapper.ToDtosAsync([undated, dated, noWhen]);

        Assert.Multiple(() =>
        {
            Assert.That(blockers.Requests.Single(), Is.EquivalentTo(new[] { undated.Id }));
            Assert.That(dtos.Select(d => d.WhenResolved), Is.EqualTo(new[] { true, true, false }));
        });
    }

    [Test]
    public async Task ToDtosAsync_WithNothingDependingOnBlockers_DoesNotAskAboutThem()
    {
        var blockers = new FakeWorkItemBlockers();
        var mapper = Mapper(blockers);

        await mapper.ToDtosAsync([NewItem()]);

        Assert.That(blockers.Requests, Is.Empty);
    }

    [Test]
    public async Task ToSwimlaneDtosAsync_ResolvesLanesAndCardsInOneLookup()
    {
        var lane = ItemWithWhen();
        var card = ItemWithWhen();
        var blockers = new FakeWorkItemBlockers(card.Id);
        var mapper = Mapper(blockers);

        var swimlanes = await mapper.ToSwimlaneDtosAsync([new Swimlane(lane, [card])]);

        Assert.Multiple(() =>
        {
            Assert.That(blockers.Requests, Has.Count.EqualTo(1));
            Assert.That(swimlanes.Single().Lane.WhenResolved, Is.False);
            Assert.That(swimlanes.Single().Cards.Single().WhenResolved, Is.True);
        });
    }

    [Test]
    public async Task ToPageDtoAsync_KeepsTheCursor_AndResolvesEachItem()
    {
        var item = ItemWithWhen();
        var mapper = Mapper(new FakeWorkItemBlockers(item.Id));

        var page = await mapper.ToPageDtoAsync(new WorkItemQueryPage<WorkItem>([item], "next"));

        Assert.Multiple(() =>
        {
            Assert.That(page.NextCursor, Is.EqualTo("next"));
            Assert.That(page.Items.Single().WhenResolved, Is.True);
        });
    }

    [Test]
    public async Task ToWriteResponseAsync_KeepsTheChangedFields_AndResolvesTheItem()
    {
        var item = ItemWithWhen();
        var mapper = Mapper(new FakeWorkItemBlockers(item.Id));

        var response = await mapper.ToWriteResponseAsync(new WorkItemWriteResult(item, [WorkItemField.When]));

        Assert.Multiple(() =>
        {
            Assert.That(response.ChangedFields, Is.EqualTo(new[] { WorkItemField.When }));
            Assert.That(response.Item.WhenResolved, Is.True);
        });
    }

    [Test]
    public async Task ToDtoAsync_CarriesTheNameBehindEachId_AlongsideTheId()
    {
        var parent = NewItem();
        var typeId = Guid.NewGuid();
        var priorityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var item = WorkItem.CreateChildOf(parent, "Paint", null, Guid.NewGuid(), rank: 1.0, typeId, priorityId);
        item.AssignTo(userId);
        var names = new WorkItemNames
        {
            Spaces = new Dictionary<Guid, string> { [item.SpaceId] = "House" },
            Statuses = new Dictionary<Guid, string> { [item.StatusId] = "Doing" },
            Types = new Dictionary<Guid, string> { [typeId] = "Chore" },
            Priorities = new Dictionary<Guid, string> { [priorityId] = "High" },
            Usernames = new Dictionary<Guid, string> { [userId] = "riley" },
            WorkItems = new Dictionary<Guid, WorkItemReference> { [parent.Id] = new(12, "Living room") },
        };
        var mapper = Mapper(new FakeWorkItemBlockers(), new FakeWorkItemNameLookup(names));

        var dto = await mapper.ToDtoAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(dto.SpaceId, Is.EqualTo(item.SpaceId));
            Assert.That(dto.SpaceName, Is.EqualTo("House"));
            Assert.That(dto.StatusName, Is.EqualTo("Doing"));
            Assert.That(dto.TypeId, Is.EqualTo(typeId));
            Assert.That(dto.TypeName, Is.EqualTo("Chore"));
            Assert.That(dto.PriorityName, Is.EqualTo("High"));
            Assert.That(dto.AssignedToUserId, Is.EqualTo(userId));
            Assert.That(dto.AssignedToUsername, Is.EqualTo("riley"));
            Assert.That(dto.ParentId, Is.EqualTo(parent.Id));
            Assert.That(dto.ParentNumber, Is.EqualTo(12));
            Assert.That(dto.ParentTitle, Is.EqualTo("Living room"));
        });
    }

    [Test]
    public async Task ToDtoAsync_WithoutTheOptionalIds_HasNoNamesForThem()
    {
        var item = NewItem();
        var names = new WorkItemNames
        {
            Spaces = new Dictionary<Guid, string> { [item.SpaceId] = "House" },
            Statuses = new Dictionary<Guid, string> { [item.StatusId] = "To Do" },
        };
        var mapper = Mapper(new FakeWorkItemBlockers(), new FakeWorkItemNameLookup(names));

        var dto = await mapper.ToDtoAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(dto.ParentNumber, Is.Null);
            Assert.That(dto.ParentTitle, Is.Null);
            Assert.That(dto.TypeName, Is.Null);
            Assert.That(dto.PriorityName, Is.Null);
            Assert.That(dto.AssignedToUsername, Is.Null);
        });
    }

    [Test]
    public async Task ToDtosAsync_LooksUpNamesOnce_HoweverManyItemsThereAre()
    {
        var oneLookup = new FakeWorkItemNameLookup();
        var manyLookup = new FakeWorkItemNameLookup();
        var many = Enumerable.Range(0, 50).Select(_ => NewItem()).ToList();

        await Mapper(new FakeWorkItemBlockers(), oneLookup).ToDtosAsync([NewItem()]);
        await Mapper(new FakeWorkItemBlockers(), manyLookup).ToDtosAsync(many);

        Assert.Multiple(() =>
        {
            Assert.That(manyLookup.Requests, Has.Count.EqualTo(oneLookup.Requests.Count).And.Count.EqualTo(1));
            Assert.That(manyLookup.Requests.Single(), Is.EquivalentTo(many));
        });
    }

    [Test]
    public async Task ToSwimlaneDtosAsync_NamesLanesAndCardsInOneLookup()
    {
        var lane = NewItem();
        var card = NewItem();
        var names = new WorkItemNames
        {
            Spaces = new Dictionary<Guid, string> { [lane.SpaceId] = "House", [card.SpaceId] = "Garden" },
        };
        var lookup = new FakeWorkItemNameLookup(names);

        var swimlanes = await Mapper(new FakeWorkItemBlockers(), lookup).ToSwimlaneDtosAsync([new Swimlane(lane, [card])]);

        Assert.Multiple(() =>
        {
            Assert.That(lookup.Requests.Single(), Is.EquivalentTo(new[] { lane, card }));
            Assert.That(swimlanes.Single().Lane.SpaceName, Is.EqualTo("House"));
            Assert.That(swimlanes.Single().Cards.Single().SpaceName, Is.EqualTo("Garden"));
        });
    }

    private static WorkItemDtoMapper Mapper(IWorkItemBlockers blockers, IWorkItemNameLookup? names = null) =>
        new(blockers, names ?? new FakeWorkItemNameLookup());

    private static WorkItem NewItem() => WorkItem.Create(Guid.NewGuid(), "Paint", null, Guid.NewGuid(), rank: 1.0);

    private static WorkItem ItemWithWhen()
    {
        var item = NewItem();
        item.UpdateDetails(new WorkItemDetailsPatch { When = "after drywall" });
        return item;
    }
}
