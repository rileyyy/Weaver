using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class StatusesControllerTests
{
    private Mock<IStatusService> _statuses = null!;
    private StatusesController _controller = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _statuses = new Mock<IStatusService>(MockBehavior.Strict);
        _userId = Guid.NewGuid();
        _controller = new StatusesController(_statuses.Object) { ControllerContext = SignedInControllerContext.For(_userId) };
    }

    [Test]
    public async Task Create_PassesTheCallerAndReturns201()
    {
        var status = Status.Create("Review", 3, StatusCategory.Doing, "#00897B");
        _statuses.Setup(s => s.CreateAsync(_userId, "Review", StatusCategory.Doing, "#00897B", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var result = await _controller.Create(new StatusDetailsRequest("Review", StatusCategory.Doing, "#00897B"));

        var created = result.Result as CreatedAtActionResult;
        Assert.Multiple(() =>
        {
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.ActionName, Is.EqualTo(nameof(StatusesController.GetAll)));
            Assert.That((created.Value as StatusDto)!.Id, Is.EqualTo(status.Id));
        });
    }

    [Test]
    public async Task Update_PassesTheCallerAndReturnsTheStatus()
    {
        var status = Status.Create("Review", 3, StatusCategory.Done, "#123456");
        _statuses.Setup(s => s.UpdateAsync(_userId, status.Id, "Review", StatusCategory.Done, "#123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var result = await _controller.Update(status.Id, new StatusDetailsRequest("Review", StatusCategory.Done, "#123456"));

        Assert.That(((result.Result as OkObjectResult)!.Value as StatusDto)!.Category, Is.EqualTo(StatusCategory.Done));
    }

    [Test]
    public async Task Reorder_ReturnsTheStatusesInTheirNewOrder()
    {
        var a = Status.Create("A", 1, StatusCategory.ToDo, "#000000");
        var b = Status.Create("B", 0, StatusCategory.ToDo, "#000000");
        _statuses.Setup(s => s.ReorderAsync(_userId, new[] { a.Id, b.Id }, It.IsAny<CancellationToken>()))
            .ReturnsAsync([a, b]);

        var result = await _controller.Reorder(new ReorderLookupRequest([a.Id, b.Id]));

        var dtos = ((result.Result as OkObjectResult)!.Value as IEnumerable<StatusDto>)!;
        Assert.That(dtos.Select(d => d.Name), Is.EqualTo(new[] { "A", "B" }));
    }

    [Test]
    public async Task Delete_PassesTheReplacementAndReturns204()
    {
        var id = Guid.NewGuid();
        var replacementId = Guid.NewGuid();
        _statuses.Setup(s => s.DeleteAsync(_userId, id, replacementId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(id, replacementId);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }
}
