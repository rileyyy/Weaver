using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemLayersControllerTests
{
    private Mock<IWorkItemLayerService> _layers = null!;
    private WorkItemLayersController _controller = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _layers = new Mock<IWorkItemLayerService>(MockBehavior.Strict);
        _userId = Guid.NewGuid();
        _controller = new WorkItemLayersController(_layers.Object) { ControllerContext = SignedInControllerContext.For(_userId) };
    }

    [Test]
    public async Task Create_PassesTheCallerAndColorAndReturns201()
    {
        var layer = WorkItemLayer.Create("Epic", 3, "#546E7A");
        _layers.Setup(l => l.CreateAsync(_userId, "Epic", "#546E7A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(layer);

        var result = await _controller.Create(new LayerDetailsRequest("Epic", "#546E7A"));

        var created = result.Result as CreatedAtActionResult;
        Assert.Multiple(() =>
        {
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.ActionName, Is.EqualTo(nameof(WorkItemLayersController.GetAll)));
            Assert.That((created.Value as WorkItemLayerDto)!.Color, Is.EqualTo("#546E7A"));
        });
    }

    [Test]
    public async Task Update_PassesTheCallerAndReturnsTheLayer()
    {
        var layer = WorkItemLayer.Create("Epic", 3, "#123456");
        _layers.Setup(l => l.UpdateAsync(_userId, layer.Id, "Epic", "#123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(layer);

        var result = await _controller.Update(layer.Id, new LayerDetailsRequest("Epic", "#123456"));

        Assert.That(((result.Result as OkObjectResult)!.Value as WorkItemLayerDto)!.Color, Is.EqualTo("#123456"));
    }
}
