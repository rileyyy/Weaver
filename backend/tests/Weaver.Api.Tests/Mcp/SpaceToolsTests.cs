using System.Reflection;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class SpaceToolsTests
{
    [Test]
    public async Task ListSpaces_DelegatesToServiceAndMapsResults()
    {
        var home = Space.Create("Home");
        home.SetDoneHidingPeriod(7);
        var spaces = new Mock<ISpaceService>(MockBehavior.Strict);
        spaces.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([home]);

        var result = await new SpaceTools(spaces.Object).ListSpaces(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Single().Name, Is.EqualTo("Home"));
            Assert.That(result.Single().DoneHidingDays, Is.EqualTo(7));
        });
    }

    [Test]
    public void EveryTool_IsReadOnly_SoAgentsCantChangeSpaces()
    {
        var tools = typeof(SpaceTools).GetMethods()
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>())
            .OfType<McpServerToolAttribute>()
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(tools, Is.Not.Empty);
            Assert.That(tools.Select(t => t.ReadOnly), Has.All.True);
        });
    }
}
