using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemUrlLinksControllerTests
{
    private Mock<IWorkItemUrlLinkService> _urlLinks = null!;
    private WorkItemUrlLinksController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _urlLinks = new Mock<IWorkItemUrlLinkService>(MockBehavior.Strict);
        _controller = new WorkItemUrlLinksController(_urlLinks.Object);
    }

    [Test]
    public async Task ListForWorkItem_DelegatesToServiceAndReturnsOk()
    {
        var workItemId = Guid.NewGuid();
        var link = new WorkItemUrlLinkView(Guid.NewGuid(), "https://example.com", "Quote");
        _urlLinks.Setup(l => l.ListForWorkItemAsync(workItemId, It.IsAny<CancellationToken>())).ReturnsAsync([link]);

        var result = await _controller.ListForWorkItem(workItemId);

        var body = ((result.Result as OkObjectResult)!.Value as IEnumerable<WorkItemUrlLinkDto>)!.Single();
        Assert.That(body, Is.EqualTo(new WorkItemUrlLinkDto(link.Id, "https://example.com", "Quote")));
    }

    [Test]
    public async Task Create_DelegatesToServiceAndReturnsCreated()
    {
        var workItemId = Guid.NewGuid();
        var link = new WorkItemUrlLinkView(Guid.NewGuid(), "https://example.com", null);
        _urlLinks.Setup(l => l.CreateAsync(workItemId, "https://example.com", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var result = await _controller.Create(workItemId, new CreateWorkItemUrlLinkRequest("https://example.com"));

        var created = result.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.ActionName, Is.EqualTo(nameof(WorkItemUrlLinksController.ListForWorkItem)));
        Assert.That(created.Value, Is.EqualTo(new WorkItemUrlLinkDto(link.Id, "https://example.com", null)));
    }

    [Test]
    public async Task Delete_DelegatesToServiceAndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _urlLinks.Setup(l => l.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Delete(id);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        _urlLinks.VerifyAll();
    }
}
