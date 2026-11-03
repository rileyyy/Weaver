using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemTypesControllerTests
{
    private Mock<IWorkItemTypeService> _types = null!;
    private WorkItemTypesController _controller = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _types = new Mock<IWorkItemTypeService>(MockBehavior.Strict);
        _userId = Guid.NewGuid();
        _controller = new WorkItemTypesController(_types.Object) { ControllerContext = SignedInControllerContext.For(_userId) };
    }

    [Test]
    public async Task Create_PassesTheCallerAndColorAndReturns201()
    {
        var spaceId = Guid.NewGuid();
        var type = WorkItemType.Create(spaceId, "Epic", 3, "#546E7A");
        _types.Setup(l => l.CreateAsync(_userId, spaceId, "Epic", "#546E7A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(type);

        var result = await _controller.Create(new CreateTypeRequest(spaceId, "Epic", "#546E7A"));

        var created = result.Result as CreatedAtActionResult;
        Assert.Multiple(() =>
        {
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.ActionName, Is.EqualTo(nameof(WorkItemTypesController.GetAll)));
            Assert.That((created.Value as WorkItemTypeDto)!.Color, Is.EqualTo("#546E7A"));
            Assert.That((created.Value as WorkItemTypeDto)!.SpaceId, Is.EqualTo(spaceId));
            Assert.That(created.RouteValues!["spaceId"], Is.EqualTo(spaceId));
        });
    }

    [Test]
    public async Task Update_PassesTheCallerAndReturnsTheType()
    {
        var type = WorkItemType.Create(Guid.NewGuid(), "Epic", 3, "#123456");
        _types.Setup(l => l.UpdateAsync(_userId, type.Id, "Epic", "#123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(type);

        var result = await _controller.Update(type.Id, new TypeDetailsRequest("Epic", "#123456"));

        Assert.That(((result.Result as OkObjectResult)!.Value as WorkItemTypeDto)!.Color, Is.EqualTo("#123456"));
    }
}
