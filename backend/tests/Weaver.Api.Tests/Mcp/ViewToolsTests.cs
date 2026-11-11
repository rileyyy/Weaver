using System.Reflection;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class ViewToolsTests
{
    [Test]
    public async Task ListViews_ListsOverviewAndSavedViews()
    {
        var saved = View.Create(new ViewDefinition(
            "Home only", [Guid.NewGuid()], null, [], TagMatch.Any, true, false, ViewLayout.Board));
        var views = new Mock<IViewService>(MockBehavior.Strict);
        views.Setup(v => v.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([View.Overview(), saved]);

        var result = await new ViewTools(views.Object).ListViews(CancellationToken.None);

        Assert.That(result.Select(v => v.Name), Is.EqualTo(new[] { "Overview", "Home only" }));
    }

    [Test]
    public async Task RunView_PassesTheWindowAndPage()
    {
        var id = Guid.NewGuid();
        var views = new Mock<IViewService>(MockBehavior.Strict);
        var window = new ViewWindow(new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15), "abc", 10);
        views
            .Setup(v => v.RunAsync(id, window, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemQueryPage<WorkItemSummary>([], "next"));

        var page = await new ViewTools(views.Object).RunView(
            id, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15), "abc", 10, CancellationToken.None);

        Assert.That(page.NextCursor, Is.EqualTo("next"));
    }

    [Test]
    public void EveryTool_IsReadOnly_SoAgentsOnlyListAndRunViews()
    {
        var tools = typeof(ViewTools).GetMethods()
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>())
            .OfType<McpServerToolAttribute>()
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(tools.Select(t => t.Name), Is.EquivalentTo(new[] { "list_views", "run_view" }));
            Assert.That(tools.All(t => t.ReadOnly), Is.True);
        });
    }
}
