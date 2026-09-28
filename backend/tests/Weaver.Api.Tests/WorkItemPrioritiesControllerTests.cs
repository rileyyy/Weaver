using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

/// <summary><see cref="WorkItemLayersController"/> has the same shape, so it isn't repeated.</summary>
[TestFixture]
public class WorkItemPrioritiesControllerTests
{
    private Mock<IWorkItemPriorityService> _priorities = null!;
    private WorkItemPrioritiesController _controller = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _priorities = new Mock<IWorkItemPriorityService>(MockBehavior.Strict);
        _userId = Guid.NewGuid();
        _controller = new WorkItemPrioritiesController(_priorities.Object)
        {
            ControllerContext = SignedInControllerContext.For(_userId),
        };
    }

    [Test]
    public async Task GetAll_ReturnsOkWithEveryPriorityAsDtos()
    {
        var low = WorkItemPriority.Create("Low", 0);
        var high = WorkItemPriority.Create("High", 1);
        _priorities.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([low, high]);

        var result = await _controller.GetAll();

        var dtos = ((result.Result as OkObjectResult)!.Value as IEnumerable<WorkItemPriorityDto>)!;
        Assert.That(dtos.Select(d => d.Name), Is.EqualTo(new[] { "Low", "High" }));
    }

    [Test]
    public async Task Create_PassesTheCallerAndReturns201()
    {
        var created = WorkItemPriority.Create("Critical", 4);
        _priorities.Setup(p => p.CreateAsync(_userId, "Critical", It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var result = await _controller.Create(new LookupNameRequest("Critical"));

        Assert.That(((result.Result as CreatedAtActionResult)!.Value as WorkItemPriorityDto)!.Id, Is.EqualTo(created.Id));
    }

    [Test]
    public async Task Rename_PassesTheCallerAndReturnsThePriority()
    {
        var renamed = WorkItemPriority.Create("Blocker", 3);
        _priorities.Setup(p => p.RenameAsync(_userId, renamed.Id, "Blocker", It.IsAny<CancellationToken>())).ReturnsAsync(renamed);

        var result = await _controller.Rename(renamed.Id, new LookupNameRequest("Blocker"));

        Assert.That(((result.Result as OkObjectResult)!.Value as WorkItemPriorityDto)!.Name, Is.EqualTo("Blocker"));
    }

    [Test]
    public async Task Delete_WithoutAReplacement_PassesNull()
    {
        var id = Guid.NewGuid();
        _priorities.Setup(p => p.DeleteAsync(_userId, id, null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Delete(id, null);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }
}
