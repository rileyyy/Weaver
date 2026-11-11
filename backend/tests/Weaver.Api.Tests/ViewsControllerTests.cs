using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class ViewsControllerTests
{
    private Mock<IViewService> _views = null!;
    private ViewsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _views = new Mock<IViewService>(MockBehavior.Strict);
        _controller = new ViewsController(_views.Object);
    }

    [Test]
    public async Task GetAll_MarksOverviewAsBuiltIn()
    {
        var saved = View.Create(Definition("Home only"));
        _views.Setup(v => v.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([View.Overview(), saved]);

        var result = await _controller.GetAll();

        var dtos = ((OkObjectResult)result.Result!).Value as IEnumerable<ViewDto>;
        Assert.That(dtos!.Select(d => (d.Name, d.BuiltIn)), Is.EqualTo(new[] { ("Overview", true), ("Home only", false) }));
    }

    [Test]
    public async Task Create_PassesTheWholeDefinition_AndReturns201PointingAtTheView()
    {
        var spaceId = Guid.NewGuid();
        var request = new SaveViewRequest(
            "Outside",
            [spaceId],
            "fence",
            ["outside"],
            TagMatch.All,
            ApplyDoneHiding: false,
            ScheduledOrInProgressOnly: true,
            ViewLayout.Roadmap);
        var expected = new ViewDefinition(
            "Outside",
            [spaceId],
            "fence",
            ["outside"],
            TagMatch.All,
            ApplyDoneHiding: false,
            ScheduledOrInProgressOnly: true,
            ViewLayout.Roadmap);
        ViewDefinition? received = null;
        _views
            .Setup(v => v.CreateAsync(It.IsAny<ViewDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<ViewDefinition, CancellationToken>((d, _) => received = d)
            .ReturnsAsync((ViewDefinition d, CancellationToken _) => View.Create(d));

        var result = await _controller.Create(request);

        var created = (CreatedAtActionResult)result.Result!;
        Assert.Multiple(() =>
        {
            Assert.That(created.ActionName, Is.EqualTo(nameof(ViewsController.GetById)));
            Assert.That(received!.SpaceIds, Is.EqualTo(expected.SpaceIds));
            Assert.That(received.Tags, Is.EqualTo(expected.Tags));
            Assert.That(received with { SpaceIds = [], Tags = [] }, Is.EqualTo(expected with { SpaceIds = [], Tags = [] }));
            Assert.That(((ViewDto)created.Value!).Layout, Is.EqualTo(ViewLayout.Roadmap));
        });
    }

    [Test]
    public async Task Delete_Returns204()
    {
        var id = Guid.NewGuid();
        _views.Setup(v => v.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Delete(id);

        Assert.That(result, Is.InstanceOf<NoContentResult>());
    }

    [Test]
    public async Task SetDefault_ReturnsTheCallersNewDefault()
    {
        var saved = View.Create(Definition("Home only"));
        _views.Setup(v => v.SetDefaultAsync(saved.Id, It.IsAny<CancellationToken>())).ReturnsAsync(saved);

        var result = await _controller.SetDefault(new SetDefaultViewRequest(saved.Id));

        Assert.That(((ViewDto)((OkObjectResult)result.Result!).Value!).Id, Is.EqualTo(saved.Id));
    }

    private static ViewDefinition Definition(string name) =>
        new(name, [], null, [], TagMatch.Any, ApplyDoneHiding: true, ScheduledOrInProgressOnly: false, ViewLayout.Board);
}
