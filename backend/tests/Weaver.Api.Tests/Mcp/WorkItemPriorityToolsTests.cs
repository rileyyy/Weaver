using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemPriorityToolsTests
{
    [Test]
    public async Task ListWorkItemPriorities_DelegatesToServiceAndMapsResults()
    {
        var priorities = new Mock<IWorkItemPriorityService>(MockBehavior.Strict);
        var spaceId = Guid.NewGuid();
        var priority = WorkItemPriority.Create(spaceId, "High", 2);
        priorities.Setup(p => p.GetAllAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync([priority]);
        var tools = new WorkItemPriorityTools(priorities.Object);

        var result = await tools.ListWorkItemPriorities(spaceId, CancellationToken.None);

        Assert.That(result.Single(), Is.EqualTo(new Contracts.WorkItemPriorityDto(priority.Id, spaceId, "High", 2)));
    }
}
