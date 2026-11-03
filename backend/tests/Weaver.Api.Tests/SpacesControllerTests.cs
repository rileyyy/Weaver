using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class SpacesControllerTests
{
    private Mock<ISpaceService> _spaces = null!;
    private SpacesController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _spaces = new Mock<ISpaceService>(MockBehavior.Strict);
        _controller = new SpacesController(_spaces.Object);
    }

    [Test]
    public async Task Create_Returns201PointingAtTheNewSpace()
    {
        var space = Space.Create("Village");
        _spaces.Setup(s => s.CreateAsync("Village", It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var result = await _controller.Create(new SpaceNameRequest("Village"));

        var created = result.Result as CreatedAtActionResult;
        Assert.Multiple(() =>
        {
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.ActionName, Is.EqualTo(nameof(SpacesController.GetById)));
            Assert.That(created.Value, Is.EqualTo(new SpaceDto(space.Id, "Village")));
        });
    }

    [Test]
    public async Task Rename_ReturnsTheRenamedSpace()
    {
        var space = Space.Create("House");
        _spaces.Setup(s => s.RenameAsync(space.Id, "House", It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var result = await _controller.Rename(space.Id, new SpaceNameRequest("House"));

        Assert.That((result.Result as OkObjectResult)!.Value, Is.EqualTo(new SpaceDto(space.Id, "House")));
    }

    [Test]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _spaces.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Space?)null);

        var result = await _controller.GetById(id);

        Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
    }
}
