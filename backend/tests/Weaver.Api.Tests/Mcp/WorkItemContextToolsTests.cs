using ModelContextProtocol;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests.Mcp;

/// <summary>
/// Every related-data service is a strict mock, so an include that wasn't asked for fails the
/// test if it's read at all, not only if it's returned.
/// </summary>
[TestFixture]
public class WorkItemContextToolsTests
{
    private Mock<IWorkItemService> _workItems = null!;
    private Mock<ICommentService> _comments = null!;
    private Mock<IWorkItemLinkService> _links = null!;
    private Mock<IWorkItemUrlLinkService> _urlLinks = null!;
    private Mock<IWorkItemHistoryService> _history = null!;
    private Mock<IWorkItemAttachmentService> _attachments = null!;
    private WorkItemContextTools _tools = null!;
    private WorkItem _item = null!;

    [SetUp]
    public void SetUp()
    {
        _workItems = new Mock<IWorkItemService>(MockBehavior.Strict);
        _comments = new Mock<ICommentService>(MockBehavior.Strict);
        _links = new Mock<IWorkItemLinkService>(MockBehavior.Strict);
        _urlLinks = new Mock<IWorkItemUrlLinkService>(MockBehavior.Strict);
        _history = new Mock<IWorkItemHistoryService>(MockBehavior.Strict);
        _attachments = new Mock<IWorkItemAttachmentService>(MockBehavior.Strict);
        _tools = new WorkItemContextTools(
            _workItems.Object,
            new WorkItemDtoMapper(new FakeWorkItemBlockers(), new FakeWorkItemNameLookup()),
            _comments.Object,
            _links.Object,
            _urlLinks.Object,
            _history.Object,
            _attachments.Object);

        _item = WorkItem.Create(Guid.NewGuid(), "Hinges", null, Guid.NewGuid(), rank: 1.0);
        _workItems.Setup(w => w.GetByIdAsync(_item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_item);
    }

    [Test]
    public async Task WithNoInclude_ReturnsOnlyTheItem()
    {
        var context = await _tools.GetWorkItemContext(_item.Id);

        Assert.Multiple(() =>
        {
            Assert.That(context.Item.Id, Is.EqualTo(_item.Id));
            Assert.That(context.Ancestors, Is.Null);
            Assert.That(context.Children, Is.Null);
            Assert.That(context.Comments, Is.Null);
            Assert.That(context.Links, Is.Null);
            Assert.That(context.UrlLinks, Is.Null);
            Assert.That(context.History, Is.Null);
            Assert.That(context.NextHistoryOffset, Is.Null);
            Assert.That(context.Attachments, Is.Null);
        });
    }

    [Test]
    public async Task ByNumber_FindsTheItem()
    {
        _workItems.Setup(w => w.GetByNumberAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(_item);

        var context = await _tools.GetWorkItemContext(number: 42);

        Assert.That(context.Item.Id, Is.EqualTo(_item.Id));
    }

    [Test]
    public void ForAnUnknownNumber_NamesTheNumber()
    {
        _workItems.Setup(w => w.GetByNumberAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync((WorkItem?)null);

        var ex = Assert.ThrowsAsync<McpException>(() => _tools.GetWorkItemContext(number: 42));

        Assert.That(ex!.Message, Does.Contain("#42"));
    }

    [Test]
    public void WithAnUnknownInclude_ListsTheValidOnes()
    {
        var ex = Assert.ThrowsAsync<McpException>(() => _tools.GetWorkItemContext(_item.Id, include: ["children", "parents"]));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("\"parents\""));
            Assert.That(ex.Message, Does.Contain("ancestors, children, comments, links, history, attachments"));
        });
    }

    [Test]
    public async Task Ancestors_AreLeanSummaries()
    {
        var root = Summary("House");
        _workItems.Setup(w => w.GetAncestorSummariesAsync(_item.Id, It.IsAny<CancellationToken>())).ReturnsAsync([root]);

        var context = await _tools.GetWorkItemContext(_item.Id, include: ["ancestors"]);

        Assert.Multiple(() =>
        {
            Assert.That(context.Ancestors, Is.EqualTo(new[] { WorkItemSummaryDto.FromSummary(root) }));
            Assert.That(context.Children, Is.Null);
        });
    }

    [Test]
    public async Task Children_AreLeanSummaries()
    {
        var child = Summary("Screws");
        _workItems.Setup(w => w.GetChildSummariesAsync(_item.Id, null, It.IsAny<CancellationToken>())).ReturnsAsync([child]);

        var context = await _tools.GetWorkItemContext(_item.Id, include: ["children"]);

        Assert.Multiple(() =>
        {
            Assert.That(context.Children, Is.EqualTo(new[] { WorkItemSummaryDto.FromSummary(child) }));
            Assert.That(context.Ancestors, Is.Null);
        });
    }

    [Test]
    public async Task Comments_AreTheItemsComments()
    {
        var comment = new CommentView(Guid.NewGuid(), _item.Id, Guid.NewGuid(), "alice", "Ordered", DateTimeOffset.UnixEpoch, null);
        _comments.Setup(c => c.ListForWorkItemAsync(_item.Id, It.IsAny<CancellationToken>())).ReturnsAsync([comment]);

        var context = await _tools.GetWorkItemContext(_item.Id, include: ["comments"]);

        Assert.That(context.Comments, Is.EqualTo(new[] { CommentDto.FromView(comment) }));
    }

    [Test]
    public async Task Links_AreTheItemsWorkItemAndUrlLinks()
    {
        var link = new WorkItemLinkView(Guid.NewGuid(), WorkItemLinkRelation.BlockedBy, Guid.NewGuid(), 7, "Cabinets", Guid.NewGuid());
        var urlLink = new WorkItemUrlLinkView(Guid.NewGuid(), "https://example.com/hinges", "Product page");
        _links.Setup(l => l.ListForWorkItemAsync(_item.Id, It.IsAny<CancellationToken>())).ReturnsAsync([link]);
        _urlLinks.Setup(l => l.ListForWorkItemAsync(_item.Id, It.IsAny<CancellationToken>())).ReturnsAsync([urlLink]);

        var context = await _tools.GetWorkItemContext(_item.Id, include: ["links"]);

        Assert.Multiple(() =>
        {
            Assert.That(context.Links, Is.EqualTo(new[] { WorkItemLinkDto.FromView(link) }));
            Assert.That(context.UrlLinks, Is.EqualTo(new[] { WorkItemUrlLinkDto.FromView(urlLink) }));
        });
    }

    [Test]
    public async Task Attachments_AreMetadataOnly()
    {
        var attachment = new WorkItemAttachmentView(Guid.NewGuid(), _item.Id, "quote.pdf", "application/pdf", 1234, DateTimeOffset.UnixEpoch);
        _attachments.Setup(a => a.ListForWorkItemAsync(_item.Id, It.IsAny<CancellationToken>())).ReturnsAsync([attachment]);

        var context = await _tools.GetWorkItemContext(_item.Id, include: ["attachments"]);

        Assert.That(context.Attachments, Is.EqualTo(new[] { AttachmentDto.FromView(attachment) }));
    }

    [Test]
    public async Task History_ReturnsTheNewestEntriesUpToTheLimit_AndWhereTheNextPageStarts()
    {
        HasHistory(5);

        var context = await _tools.GetWorkItemContext(_item.Id, include: ["history"], historyLimit: 2);

        Assert.Multiple(() =>
        {
            Assert.That(context.History!.Select(e => e.Id), Is.EqualTo(new long[] { 5, 4 }));
            Assert.That(context.NextHistoryOffset, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task History_FromAnOffset_ReturnsTheNextPage_AndNoNextOffsetOnTheLast()
    {
        HasHistory(5);

        var context = await _tools.GetWorkItemContext(_item.Id, include: ["history"], historyOffset: 4, historyLimit: 2);

        Assert.Multiple(() =>
        {
            Assert.That(context.History!.Select(e => e.Id), Is.EqualTo(new long[] { 1 }));
            Assert.That(context.NextHistoryOffset, Is.Null);
        });
    }

    [Test]
    public async Task History_ByDefault_IsLimitedTo20()
    {
        HasHistory(25);

        var context = await _tools.GetWorkItemContext(_item.Id, include: ["history"]);

        Assert.Multiple(() =>
        {
            Assert.That(context.History, Has.Count.EqualTo(20));
            Assert.That(context.NextHistoryOffset, Is.EqualTo(20));
        });
    }

    [TestCase(0, 0)]
    [TestCase(0, 201)]
    [TestCase(-1, 10)]
    public void History_WithAnInvalidPage_IsRejected(int offset, int limit)
    {
        Assert.ThrowsAsync<McpException>(() =>
            _tools.GetWorkItemContext(_item.Id, include: ["history"], historyOffset: offset, historyLimit: limit));
    }

    [Test]
    public async Task Includes_AreCaseInsensitive_AndCanBeCombined()
    {
        _workItems.Setup(w => w.GetChildSummariesAsync(_item.Id, null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _comments.Setup(c => c.ListForWorkItemAsync(_item.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var context = await _tools.GetWorkItemContext(_item.Id, include: ["Children", " COMMENTS "]);

        Assert.Multiple(() =>
        {
            Assert.That(context.Children, Is.Empty);
            Assert.That(context.Comments, Is.Empty);
            Assert.That(context.History, Is.Null);
        });
    }

    private void HasHistory(int count)
    {
        var newestFirst = Enumerable.Range(1, count).Reverse()
            .Select(id => new WorkItemHistoryView(
                id, _item.Id, DateTimeOffset.UnixEpoch, null, null, null, null, null, null, "title", null, null, null, null, null))
            .ToList();
        _history.Setup(h => h.ListForWorkItemAsync(_item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(newestFirst);
    }

    private static WorkItemSummary Summary(string title) =>
        new(Guid.NewGuid(), 7, title, Guid.NewGuid(), "Home", null, "To Do", "Task", null, null, null, 1);
}
