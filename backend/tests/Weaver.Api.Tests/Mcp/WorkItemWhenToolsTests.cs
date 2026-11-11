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
public class WorkItemWhenToolsTests
{
    [Test]
    public async Task ListUnresolvedWhenItems_AsksForUnresolvedWhensOnly_AndReturnsSummariesWithThePhrase()
    {
        var spaceId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var summary = new WorkItemSummary(
            Guid.NewGuid(), 12, "Paint the hall", spaceId, "Home", parentId, "To Do", null, null, null, null, 0,
            When: "after drywall");
        WorkItemQuery? received = null;
        var service = new Mock<IWorkItemQueryService>(MockBehavior.Strict);
        service
            .Setup(s => s.QuerySummariesAsync(It.IsAny<WorkItemQuery>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItemQuery, CancellationToken>((q, _) => received = q)
            .ReturnsAsync(new WorkItemQueryPage<WorkItemSummary>([summary], "next"));

        var page = await new WorkItemWhenTools(service.Object).ListUnresolvedWhenItems(
            spaceIds: [spaceId],
            statusCategories: [StatusCategory.ToDo, StatusCategory.Doing],
            cursor: "abc",
            limit: 5);

        var item = page.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.When, Is.EqualTo("after drywall"));
            Assert.That(item.SpaceName, Is.EqualTo("Home"));
            Assert.That(item.ParentId, Is.EqualTo(parentId));
            Assert.That(page.NextCursor, Is.EqualTo("next"));
            Assert.That(received!.UnresolvedWhenOnly, Is.True);
            Assert.That(received.SpaceIds, Is.EqualTo(new[] { spaceId }));
            Assert.That(received.StatusCategories, Is.EqualTo(new[] { StatusCategory.ToDo, StatusCategory.Doing }));
            Assert.That(received.Cursor, Is.EqualTo("abc"));
            Assert.That(received.Limit, Is.EqualTo(5));
            Assert.That(received.IncludeDeleted, Is.False);
            Assert.That(received.IncludeEditedWhen, Is.False);
            Assert.That(item.WhenEditedSinceResolved, Is.False);
        });
    }

    [Test]
    public async Task ListUnresolvedWhenItems_WithIncludeEdited_AsksForEditedItemsToo_AndMarksThem()
    {
        var summary = new WorkItemSummary(
            Guid.NewGuid(), 12, "Paint the hall", Guid.NewGuid(), "Home", null, "To Do", null,
            new DateOnly(2026, 11, 10), null, null, 0, When: "next summer", WhenEditedSinceResolved: true);
        WorkItemQuery? received = null;
        var service = new Mock<IWorkItemQueryService>(MockBehavior.Strict);
        service
            .Setup(s => s.QuerySummariesAsync(It.IsAny<WorkItemQuery>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItemQuery, CancellationToken>((q, _) => received = q)
            .ReturnsAsync(new WorkItemQueryPage<WorkItemSummary>([summary], null));

        var page = await new WorkItemWhenTools(service.Object).ListUnresolvedWhenItems(includeEdited: true);

        Assert.Multiple(() =>
        {
            Assert.That(received!.UnresolvedWhenOnly, Is.True);
            Assert.That(received.IncludeEditedWhen, Is.True);
            Assert.That(page.Items.Single().WhenEditedSinceResolved, Is.True);
        });
    }

    [Test]
    public void ListUnresolvedWhenItems_ReportsABadArgumentAsAToolError()
    {
        var service = new Mock<IWorkItemQueryService>(MockBehavior.Strict);
        service
            .Setup(s => s.QuerySummariesAsync(It.IsAny<WorkItemQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("Limit must be between 1 and 1000."));

        var ex = Assert.ThrowsAsync<McpException>(() => new WorkItemWhenTools(service.Object).ListUnresolvedWhenItems(limit: 0));

        Assert.That(ex!.Message, Is.EqualTo("Limit must be between 1 and 1000."));
    }

    [Test]
    public void ListUnresolvedWhenItems_IsReadOnly_SoReadOnlyKeysCanAskIt()
    {
        var tool = ToolMethod().GetCustomAttribute<McpServerToolAttribute>()!;

        Assert.Multiple(() =>
        {
            Assert.That(tool.Name, Is.EqualTo("list_unresolved_when_items"));
            Assert.That(tool.ReadOnly, Is.True);
        });
    }

    // Weaver has no LLM (ADR-0003): the description is all that steers an agent away from
    // linking to a guessed item or guessing at a phrase only a human can read.
    [Test]
    public void ListUnresolvedWhenItems_TellsAgentsToSearchBeforeLinking_AndToLeaveAmbiguousPhrases()
    {
        var description = ToolMethod().GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()!.Description;

        Assert.Multiple(() =>
        {
            Assert.That(description, Does.Contain("search_work_items"));
            Assert.That(description, Does.Contain("reschedule_work_item"));
            Assert.That(description, Does.Contain("create_work_item_link"));
            Assert.That(description, Does.Contain("ambiguous").IgnoreCase);
        });
    }

    private static MethodInfo ToolMethod() =>
        typeof(WorkItemWhenTools).GetMethod(nameof(WorkItemWhenTools.ListUnresolvedWhenItems))!;
}
