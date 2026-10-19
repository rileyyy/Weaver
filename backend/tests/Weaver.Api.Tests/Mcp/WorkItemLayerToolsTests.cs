using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemLayerToolsTests
{
    [Test]
    public async Task ListWorkItemLayers_DelegatesToServiceAndMapsResults()
    {
        var layers = new Mock<IWorkItemLayerService>(MockBehavior.Strict);
        var layer = WorkItemLayer.Create("Project", 0, "#D81B60");
        layers.Setup(l => l.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([layer]);
        var tools = new WorkItemLayerTools(layers.Object);

        var result = await tools.ListWorkItemLayers(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Single().Id, Is.EqualTo(layer.Id));
            Assert.That(result.Single().Color, Is.EqualTo("#D81B60"));
        });
    }
}
