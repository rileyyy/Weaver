using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;
using ModelContextProtocol;
using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

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

        var result = await new SpaceTools(spaces.Object, Mock.Of<ISpaceHistoryService>(MockBehavior.Strict))
            .ListSpaces(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Single().Name, Is.EqualTo("Home"));
            Assert.That(result.Single().DoneHidingDays, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task ListSpaceHistory_DelegatesToServiceAndMapsResults()
    {
        var spaceId = Guid.NewGuid();
        var renamed = new SpaceHistoryView(
            2, spaceId, DateTimeOffset.UnixEpoch, Guid.NewGuid(), "admin", UserKind.Human, null, null, null,
            SpaceHistoryField.Name, JsonSerializer.SerializeToElement("Home"), JsonSerializer.SerializeToElement("House"));
        var history = new Mock<ISpaceHistoryService>(MockBehavior.Strict);
        history.Setup(h => h.ListForSpaceAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync([renamed]);

        var result = await new SpaceTools(Mock.Of<ISpaceService>(MockBehavior.Strict), history.Object)
            .ListSpaceHistory(spaceId, CancellationToken.None);

        var entry = result.Single();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Field, Is.EqualTo(SpaceHistoryField.Name));
            Assert.That(entry.AccountName, Is.EqualTo("admin"));
            Assert.That(entry.NewValue?.GetString(), Is.EqualTo("House"));
        });
    }

    [Test]
    public void ListSpaceHistory_AnUnknownSpace_IsAToolError()
    {
        var spaceId = Guid.NewGuid();
        var history = new Mock<ISpaceHistoryService>(MockBehavior.Strict);
        history.Setup(h => h.ListForSpaceAsync(spaceId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException(nameof(Space), spaceId));

        var tools = new SpaceTools(Mock.Of<ISpaceService>(MockBehavior.Strict), history.Object);

        Assert.ThrowsAsync<McpException>(() => tools.ListSpaceHistory(spaceId, CancellationToken.None));
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
