using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemTypeToolsTests
{
    [Test]
    public async Task ListWorkItemTypes_DelegatesToServiceAndMapsResults()
    {
        var types = new Mock<IWorkItemTypeService>(MockBehavior.Strict);
        var spaceId = Guid.NewGuid();
        var type = WorkItemType.Create(spaceId, "Project", 0, "#D81B60");
        types.Setup(l => l.GetAllAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync([type]);
        var tools = new WorkItemTypeTools(types.Object);

        var result = await tools.ListWorkItemTypes(spaceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Single().Id, Is.EqualTo(type.Id));
            Assert.That(result.Single().Color, Is.EqualTo("#D81B60"));
            Assert.That(result.Single().SpaceId, Is.EqualTo(spaceId));
        });
    }
}
