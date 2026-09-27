using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemPrioritiesControllerTests
{
    [Test]
    public async Task GetAll_ReturnsOkWithEveryPriorityAsDtos()
    {
        var priorities = new Mock<IWorkItemPriorityService>(MockBehavior.Strict);
        var low = new WorkItemPriority { Id = Guid.NewGuid(), Name = "Low", Order = 0 };
        var high = new WorkItemPriority { Id = Guid.NewGuid(), Name = "High", Order = 1 };
        priorities.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([low, high]);
        var controller = new WorkItemPrioritiesController(priorities.Object);

        var result = await controller.GetAll();

        var dtos = ((result.Result as OkObjectResult)!.Value as IEnumerable<WorkItemPriorityDto>)!;
        Assert.That(dtos.Select(d => d.Name), Is.EqualTo(new[] { "Low", "High" }));
    }
}
