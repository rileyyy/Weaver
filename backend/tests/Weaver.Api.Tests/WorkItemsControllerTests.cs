using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Domain;
using Weaver.Infrastructure.Services;

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
        _controller = new WorkItemsController(_workItems.Object);
    }

    private static WorkItem MakeWorkItem(Guid? parentId = null, Guid statusId = default) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Task",
        ParentId = parentId,
        StatusId = statusId,
        Rank = 1.0,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

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
    public async Task GetAll_ReturnsOkWithEveryItem()
    {
        var items = new[] { MakeWorkItem(), MakeWorkItem() };
        _workItems.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(items);

        var result = await _controller.GetAll();

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
        var created = MakeWorkItem(parentId, statusId);

        _workItems.Setup(s => s.CreateAsync(
                "Title", "Desc", parentId, statusId, afterId, null, WorkItemPriority.Medium, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _controller.Create(new CreateWorkItemRequest("Title", "Desc", parentId, statusId, afterId));

        var createdResult = result.Result as CreatedAtActionResult;
        Assert.That(createdResult, Is.Not.Null);
        Assert.That((createdResult!.Value as WorkItemDto)!.Id, Is.EqualTo(created.Id));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task ChangeStatus_CallsServiceWithoutTouchingParent_AndReturnsOk()
    {
        var item = MakeWorkItem();
        var newStatusId = Guid.NewGuid();

        _workItems.Setup(s => s.ChangeStatusAsync(item.Id, newStatusId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var result = await _controller.ChangeStatus(item.Id, new ChangeWorkItemStatusRequest(newStatusId, null));

        Assert.That((result.Result as OkObjectResult)!.Value, Is.EqualTo(WorkItemDto.FromEntity(item)));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task Reparent_CallsServiceWithoutTouchingStatus_AndReturnsOk()
    {
        var item = MakeWorkItem();
        var newParentId = Guid.NewGuid();

        _workItems.Setup(s => s.ReparentAsync(item.Id, newParentId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var result = await _controller.Reparent(item.Id, new ReparentWorkItemRequest(newParentId, null));

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        _workItems.VerifyAll();
    }

    [Test]
    public async Task Reschedule_DelegatesToService_AndReturnsOk()
    {
        var item = MakeWorkItem();
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(3);

        _workItems.Setup(s => s.RescheduleAsync(item.Id, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var result = await _controller.Reschedule(item.Id, new RescheduleWorkItemRequest(start, end));

        Assert.That((result.Result as OkObjectResult)!.Value, Is.EqualTo(WorkItemDto.FromEntity(item)));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task Delete_DelegatesCascadeFlagToService_AndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _workItems.Setup(s => s.DeleteAsync(id, true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Delete(id, cascade: true);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        _workItems.VerifyAll();
    }

    [Test]
    public async Task UpdateDetails_DelegatesToService_AndReturnsOk()
    {
        var item = MakeWorkItem();
        var layerId = Guid.NewGuid();
        _workItems.Setup(s => s.UpdateDetailsAsync(
                item.Id, "New title", "New description", layerId, WorkItemPriority.High, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var result = await _controller.UpdateDetails(
            item.Id, new UpdateWorkItemDetailsRequest("New title", "New description", layerId, WorkItemPriority.High));

        Assert.That((result.Result as OkObjectResult)!.Value, Is.EqualTo(WorkItemDto.FromEntity(item)));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task Assign_DelegatesToService_AndReturnsOk()
    {
        var item = MakeWorkItem();
        var userId = Guid.NewGuid();
        _workItems.Setup(s => s.AssignAsync(item.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _controller.Assign(item.Id, new AssignWorkItemRequest(userId));

        Assert.That((result.Result as OkObjectResult)!.Value, Is.EqualTo(WorkItemDto.FromEntity(item)));
        _workItems.VerifyAll();
    }
}
