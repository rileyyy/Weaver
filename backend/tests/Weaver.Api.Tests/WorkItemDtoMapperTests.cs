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
        var mapper = new WorkItemDtoMapper(new FakeWorkItemBlockers(item.Id));

        var dto = await mapper.ToDtoAsync(item);

        Assert.That(dto.WhenResolved, Is.True);
    }

    [Test]
    public async Task ToDtoAsync_AWhenWithNoDatesOrBlocker_IsUnresolved()
    {
        var item = ItemWithWhen();
        var mapper = new WorkItemDtoMapper(new FakeWorkItemBlockers());

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
        var mapper = new WorkItemDtoMapper(blockers);

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
        var mapper = new WorkItemDtoMapper(blockers);

        await mapper.ToDtosAsync([NewItem()]);

        Assert.That(blockers.Requests, Is.Empty);
    }

    [Test]
    public async Task ToSwimlaneDtosAsync_ResolvesLanesAndCardsInOneLookup()
    {
        var lane = ItemWithWhen();
        var card = ItemWithWhen();
        var blockers = new FakeWorkItemBlockers(card.Id);
        var mapper = new WorkItemDtoMapper(blockers);

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
        var mapper = new WorkItemDtoMapper(new FakeWorkItemBlockers(item.Id));

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
        var mapper = new WorkItemDtoMapper(new FakeWorkItemBlockers(item.Id));

        var response = await mapper.ToWriteResponseAsync(new WorkItemWriteResult(item, [WorkItemField.When]));

        Assert.Multiple(() =>
        {
            Assert.That(response.ChangedFields, Is.EqualTo(new[] { WorkItemField.When }));
            Assert.That(response.Item.WhenResolved, Is.True);
        });
    }

    private static WorkItem NewItem() => WorkItem.Create(Guid.NewGuid(), "Paint", null, Guid.NewGuid(), rank: 1.0);

    private static WorkItem ItemWithWhen()
    {
        var item = NewItem();
        item.UpdateDetails(new WorkItemDetailsPatch { When = "after drywall" });
        return item;
    }
}
