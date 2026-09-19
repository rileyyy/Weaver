using Moq;
using Weaver.Api.Mcp;
using Weaver.Domain;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemLayerToolsTests
{
    [Test]
    public async Task ListWorkItemLayers_DelegatesToServiceAndMapsResults()
    {
        var layers = new Mock<IWorkItemLayerService>(MockBehavior.Strict);
        var layer = new WorkItemLayer { Id = Guid.NewGuid(), Name = "Project", Order = 0 };
        layers.Setup(l => l.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([layer]);
        var tools = new WorkItemLayerTools(layers.Object);

        var result = await tools.ListWorkItemLayers(CancellationToken.None);

        Assert.That(result.Single().Id, Is.EqualTo(layer.Id));
    }
}
