using System.Reflection;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemChangesToolsTests
{
    private static readonly DateTimeOffset Since = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ListChangesSince_PassesTheQueryThrough_AndMapsThePage()
    {
        var spaceId = Guid.NewGuid();
        var deleted = new WorkItemSummary(Guid.NewGuid(), null, "Old shed", spaceId, "Home", null, null, null, null, null, null);
        var service = new Mock<IWorkItemChangesService>(MockBehavior.Strict);
        service
            .Setup(s => s.ListSinceAsync(new WorkItemChangesQuery(Since, spaceId, true, "abc", 10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemChangesPage([new ChangedWorkItem(deleted, Deleted: true, [])], "next"));

        var page = await new WorkItemChangesTools(service.Object).ListChangesSince(Since, spaceId, true, "abc", 10);

        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Single().Item.Title, Is.EqualTo("Old shed"));
            Assert.That(page.Items.Single().Deleted, Is.True);
            Assert.That(page.NextCursor, Is.EqualTo("next"));
        });
    }

    [Test]
    public void ListChangesSince_ReportsABadArgumentAsAToolError()
    {
        var service = new Mock<IWorkItemChangesService>(MockBehavior.Strict);
        service
            .Setup(s => s.ListSinceAsync(It.IsAny<WorkItemChangesQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("The cursor isn't one this server issued."));

        var ex = Assert.ThrowsAsync<McpException>(() =>
            new WorkItemChangesTools(service.Object).ListChangesSince(Since, cursor: "garbage"));

        Assert.That(ex!.Message, Is.EqualTo("The cursor isn't one this server issued."));
    }

    [Test]
    public void ListChangesSince_IsReadOnly_SoReadOnlyKeysCanAskIt()
    {
        var tool = typeof(WorkItemChangesTools).GetMethod(nameof(WorkItemChangesTools.ListChangesSince))!
            .GetCustomAttribute<McpServerToolAttribute>()!;

        Assert.That(tool.ReadOnly, Is.True);
    }
}
