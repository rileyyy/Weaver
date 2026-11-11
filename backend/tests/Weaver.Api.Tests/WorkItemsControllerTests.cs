using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemsControllerTests
{
    private Mock<IWorkItemService> _workItems = null!;
    private WorkItemsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _workItems = new Mock<IWorkItemService>(MockBehavior.Strict);
        _controller = new WorkItemsController(_workItems.Object, new WorkItemDtoMapper(new FakeWorkItemBlockers(), new FakeWorkItemNameLookup(), new FakeWorkItemWhenEdits()));
    }

    private static WorkItem MakeWorkItem() => TestWorkItems.Create("Task");

    [Test]
    public async Task GetById_WhenServiceReturnsNull_ReturnsNotFound()
    {
        _workItems.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem?)null);

        var result = await _controller.GetById(Guid.NewGuid());

        Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task GetById_WhenFound_ReturnsOkWithDto()
    {
        var item = MakeWorkItem();
        _workItems.Setup(s => s.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _controller.GetById(item.Id);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var dto = ok!.Value as WorkItemDto;
        Assert.That(dto!.Id, Is.EqualTo(item.Id));
    }

    [Test]
    public async Task GetAll_ReturnsOkWithTheRequestedPage()
    {
        var items = new[] { MakeWorkItem(), MakeWorkItem() };
        var spaceId = Guid.NewGuid();
        _workItems.Setup(s => s.GetAllAsync(500, 2, spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(items);

        var result = await _controller.GetAll(offset: 500, limit: 2, spaceId);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var dtos = (ok!.Value as IEnumerable<WorkItemDto>)!.ToList();
        Assert.That(dtos.Select(d => d.Id), Is.EquivalentTo(items.Select(i => i.Id)));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task Create_DelegatesToServiceAndReturnsCreatedAtAction()
    {
        var parentId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var afterId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var created = MakeWorkItem();

        _workItems.Setup(s => s.CreateAsync(
                "Title", "Desc", parentId, statusId, afterId, null, null, spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _controller.Create(
            new CreateWorkItemRequest("Title", "Desc", parentId, statusId, afterId, SpaceId: spaceId));

        var createdResult = result.Result as CreatedAtActionResult;
        Assert.That(createdResult, Is.Not.Null);
        Assert.That((createdResult!.Value as WorkItemDto)!.Id, Is.EqualTo(created.Id));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task ChangeStatus_CallsServiceWithoutTouchingParent_AndReturnsTheWrite()
    {
        var item = MakeWorkItem();
        var newStatusId = Guid.NewGuid();
        var written = new WorkItemWriteResult(item, [WorkItemField.StatusId]);

        _workItems.Setup(s => s.ChangeStatusAsync(item.Id, newStatusId, null, 7u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(written);

        var result = await _controller.ChangeStatus(item.Id, new ChangeWorkItemStatusRequest(newStatusId, null, 7u));

        var response = OkValue(result);
        Assert.That(response.Item, Is.EqualTo(WorkItemDto.FromEntity(item, blockers: [], WorkItemNames.None)));
        Assert.That(response.ChangedFields, Is.EqualTo(new[] { WorkItemField.StatusId }));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task Reparent_CallsServiceWithoutTouchingStatus_AndReturnsOk()
    {
        var item = MakeWorkItem();
        var newParentId = Guid.NewGuid();

        _workItems.Setup(s => s.ReparentAsync(item.Id, newParentId, null, 7u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, []));

        var result = await _controller.Reparent(item.Id, new ReparentWorkItemRequest(newParentId, null, 7u));

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        _workItems.VerifyAll();
    }

    [Test]
    public async Task Reschedule_PassesThePatchAndExpectedVersionThrough()
    {
        var item = MakeWorkItem();
        var end = new DateOnly(2026, 9, 25);

        _workItems.Setup(s => s.RescheduleAsync(
                item.Id, Optional<DateOnly?>.Unset, new Optional<DateOnly?>(end), 7u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.EndDate]));

        var result = await _controller.Reschedule(
            item.Id, new RescheduleWorkItemRequest(Optional<DateOnly?>.Unset, end, 7u));

        Assert.That(OkValue(result).ChangedFields, Is.EqualTo(new[] { WorkItemField.EndDate }));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task UpdateDetails_PassesOnlyTheSentFieldsThrough()
    {
        var item = MakeWorkItem();
        var expectedPatch = new WorkItemDetailsPatch { Title = "New title", Description = null };
        _workItems.Setup(s => s.UpdateDetailsAsync(item.Id, expectedPatch, 7u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.Title]));

        var result = await _controller.UpdateDetails(
            item.Id,
            new UpdateWorkItemDetailsRequest(
                "New title", new Optional<string?>(null), Optional<Guid?>.Unset, Optional<Guid?>.Unset, Optional<string?>.Unset, 7u));

        Assert.That(OkValue(result).ChangedFields, Is.EqualTo(new[] { WorkItemField.Title }));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task UpdateDetails_PassesTheWhenThrough()
    {
        var item = MakeWorkItem();
        var expectedPatch = new WorkItemDetailsPatch { When = "after drywall" };
        _workItems.Setup(s => s.UpdateDetailsAsync(item.Id, expectedPatch, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.When]));

        await _controller.UpdateDetails(
            item.Id,
            new UpdateWorkItemDetailsRequest(
                Optional<string>.Unset, Optional<string?>.Unset, Optional<Guid?>.Unset, Optional<Guid?>.Unset, "after drywall"));

        _workItems.VerifyAll();
    }

    [Test]
    public async Task Assign_DelegatesToServiceWithExpectedVersion_AndReturnsOk()
    {
        var item = MakeWorkItem();
        var userId = Guid.NewGuid();
        _workItems.Setup(s => s.AssignAsync(item.Id, userId, 7u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.AssignedToUserId]));

        var result = await _controller.Assign(item.Id, new AssignWorkItemRequest(userId, 7u));

        Assert.That(OkValue(result).Item, Is.EqualTo(WorkItemDto.FromEntity(item, blockers: [], WorkItemNames.None)));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task SetTags_DelegatesToServiceWithExpectedVersion_AndReturnsOk()
    {
        var item = MakeWorkItem();
        var tags = new List<string> { "urgent", "needs review" };
        _workItems.Setup(s => s.SetTagsAsync(
                item.Id,
                It.Is<Optional<IReadOnlyList<string>>>(t => t.HasValue && t.Value.SequenceEqual(tags)),
                7u,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.Tags]));

        var result = await _controller.SetTags(item.Id, new SetTagsWorkItemRequest(tags, 7u));

        Assert.That(OkValue(result).Item, Is.EqualTo(WorkItemDto.FromEntity(item, blockers: [], WorkItemNames.None)));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task SetCustomFields_DelegatesTheValuesAndExpectedVersion_AndReturnsTheChangedFields()
    {
        var item = MakeWorkItem();
        var fieldId = Guid.NewGuid();
        var values = new Dictionary<Guid, JsonElement> { [fieldId] = JsonSerializer.SerializeToElement(1200) };
        _workItems.Setup(s => s.SetCustomFieldsAsync(item.Id, values, 7u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.CustomField(fieldId)]));

        var result = await _controller.SetCustomFields(item.Id, new SetCustomFieldsRequest(values, 7u));

        Assert.That(OkValue(result).ChangedFields, Is.EqualTo(new[] { $"fields.{fieldId}" }));
    }

    [Test]
    public async Task SetTags_WithNullTags_ClearsThem()
    {
        var item = MakeWorkItem();
        _workItems.Setup(s => s.SetTagsAsync(
                item.Id,
                It.Is<Optional<IReadOnlyList<string>>>(t => t.HasValue && t.Value.Count == 0),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, []));

        await _controller.SetTags(item.Id, new SetTagsWorkItemRequest(new Optional<IReadOnlyList<string>?>(null)));

        _workItems.VerifyAll();
    }

    [Test]
    public async Task SetTags_WithTagsLeftOut_LeavesThemUnchanged()
    {
        var item = MakeWorkItem();
        _workItems.Setup(s => s.SetTagsAsync(
                item.Id, Optional<IReadOnlyList<string>>.Unset, 7u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, []));

        await _controller.SetTags(item.Id, new SetTagsWorkItemRequest(Optional<IReadOnlyList<string>?>.Unset, 7u));

        _workItems.VerifyAll();
    }

    [Test]
    public async Task GetSwimlanes_MapsLanesAndCardsToDtos()
    {
        var lane = MakeWorkItem();
        var card = MakeWorkItem();
        var scopeId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        _workItems.Setup(s => s.GetSwimlanesAsync(scopeId, spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Swimlane(lane, new[] { card }) });

        var result = await _controller.GetSwimlanes(scopeId, spaceId);

        var dtos = ((result.Result as OkObjectResult)!.Value as IEnumerable<SwimlaneDto>)!.ToList();
        Assert.That(dtos.Single().Lane, Is.EqualTo(WorkItemDto.FromEntity(lane, blockers: [], WorkItemNames.None)));
        Assert.That(dtos.Single().Cards.Single(), Is.EqualTo(WorkItemDto.FromEntity(card, blockers: [], WorkItemNames.None)));
    }

    private static WorkItemWriteResponse OkValue(ActionResult<WorkItemWriteResponse> result) =>
        (WorkItemWriteResponse)((OkObjectResult)result.Result!).Value!;
}
