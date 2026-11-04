using System.Reflection;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemSearchToolsTests
{
    [Test]
    public async Task SearchWorkItems_PassesEveryInputThrough_AndReturnsSummaries()
    {
        var spaceId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var since = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        var summary = new WorkItemSummary(Guid.NewGuid(), 12, "Paint the fence", spaceId, "Home", "Doing", "Task", null, null, "alice", 0);
        WorkItemQuery? received = null;
        var service = new Mock<IWorkItemQueryService>(MockBehavior.Strict);
        service
            .Setup(s => s.QuerySummariesAsync(It.IsAny<WorkItemQuery>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItemQuery, CancellationToken>((q, _) => received = q)
            .ReturnsAsync(new WorkItemQueryPage<WorkItemSummary>([summary], "next"));

        var page = await new WorkItemSearchTools(service.Object).SearchWorkItems(
            spaceIds: [spaceId],
            text: "paint",
            number: 12,
            statusCategories: [StatusCategory.Doing],
            assigneeId: assigneeId,
            tags: ["outside"],
            tagMatch: TagMatch.All,
            typeIds: [typeId],
            from: new DateOnly(2026, 11, 1),
            to: new DateOnly(2026, 11, 30),
            backlogOnly: true,
            updatedSince: since,
            cursor: "abc",
            limit: 5);

        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Single().Title, Is.EqualTo("Paint the fence"));
            Assert.That(page.Items.Single().Number, Is.EqualTo(12));
            Assert.That(page.NextCursor, Is.EqualTo("next"));
            Assert.That(received!.SpaceIds, Is.EqualTo(new[] { spaceId }));
            Assert.That(received.Text, Is.EqualTo("paint"));
            Assert.That(received.Number, Is.EqualTo(12));
            Assert.That(received.StatusCategories, Is.EqualTo(new[] { StatusCategory.Doing }));
            Assert.That(received.AssigneeId, Is.EqualTo(assigneeId));
            Assert.That(received.Tags, Is.EqualTo(new[] { "outside" }));
            Assert.That(received.TagMatch, Is.EqualTo(TagMatch.All));
            Assert.That(received.TypeIds, Is.EqualTo(new[] { typeId }));
            Assert.That(received.From, Is.EqualTo(new DateOnly(2026, 11, 1)));
            Assert.That(received.To, Is.EqualTo(new DateOnly(2026, 11, 30)));
            Assert.That(received.BacklogOnly, Is.True);
            Assert.That(received.UpdatedSince, Is.EqualTo(since));
            Assert.That(received.Cursor, Is.EqualTo("abc"));
            Assert.That(received.Limit, Is.EqualTo(5));
        });
    }

    [Test]
    public void SearchWorkItems_ReportsABadArgumentAsAToolError()
    {
        var service = new Mock<IWorkItemQueryService>(MockBehavior.Strict);
        service
            .Setup(s => s.QuerySummariesAsync(It.IsAny<WorkItemQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("The date window can't end before it starts."));

        var ex = Assert.ThrowsAsync<McpException>(() => new WorkItemSearchTools(service.Object).SearchWorkItems());

        Assert.That(ex!.Message, Is.EqualTo("The date window can't end before it starts."));
    }

    [Test]
    public void SearchWorkItems_IsReadOnly_SoReadOnlyKeysCanAskIt()
    {
        var tool = typeof(WorkItemSearchTools).GetMethod(nameof(WorkItemSearchTools.SearchWorkItems))!
            .GetCustomAttribute<McpServerToolAttribute>()!;

        Assert.Multiple(() =>
        {
            Assert.That(tool.Name, Is.EqualTo("search_work_items"));
            Assert.That(tool.ReadOnly, Is.True);
        });
    }
}
