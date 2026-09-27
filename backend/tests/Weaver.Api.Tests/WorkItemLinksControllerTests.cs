using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemLinksControllerTests
{
    private Mock<IWorkItemLinkService> _links = null!;
    private WorkItemLinksController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _links = new Mock<IWorkItemLinkService>(MockBehavior.Strict);
        _controller = new WorkItemLinksController(_links.Object);
    }

    private static WorkItemLinkView MakeLink(Guid linkedWorkItemId) => new(Guid.NewGuid(), linkedWorkItemId, "B");

    [Test]
    public async Task ListForWorkItem_DelegatesToServiceAndReturnsOk()
    {
        var workItemId = Guid.NewGuid();
        var link = MakeLink(Guid.NewGuid());
        _links.Setup(l => l.ListForWorkItemAsync(workItemId, It.IsAny<CancellationToken>())).ReturnsAsync([link]);

        var result = await _controller.ListForWorkItem(workItemId);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var body = (ok!.Value as IEnumerable<WorkItemLinkDto>)!.ToList();
        Assert.That(body.Single().LinkedWorkItemTitle, Is.EqualTo("B"));
    }

    [Test]
    public async Task Create_DelegatesToServiceAndReturnsCreated()
    {
        var workItemId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var link = MakeLink(targetId);
        _links.Setup(l => l.CreateAsync(workItemId, targetId, It.IsAny<CancellationToken>())).ReturnsAsync(link);

        var result = await _controller.Create(workItemId, new CreateWorkItemLinkRequest(targetId));

        var created = result.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.ActionName, Is.EqualTo(nameof(WorkItemLinksController.ListForWorkItem)));
        Assert.That((created.Value as WorkItemLinkDto)!.LinkedWorkItemId, Is.EqualTo(targetId));
    }

    [Test]
    public async Task Delete_DelegatesToServiceAndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _links.Setup(l => l.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Delete(id);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        _links.VerifyAll();
    }
}
