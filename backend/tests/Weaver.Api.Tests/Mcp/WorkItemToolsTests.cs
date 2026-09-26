using ModelContextProtocol;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Mcp;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemToolsTests
{
    private Mock<IWorkItemService> _workItems = null!;
    private WorkItemTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _workItems = new Mock<IWorkItemService>(MockBehavior.Strict);
        _tools = new WorkItemTools(_workItems.Object);
    }

    private static WorkItem MakeWorkItem(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Title = "A work item",
        StatusId = Guid.NewGuid(),
    };

    [Test]
    public async Task GetWorkItem_WhenFound_ReturnsDto()
    {
        var item = MakeWorkItem();
        _workItems.Setup(w => w.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _tools.GetWorkItem(item.Id, CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo(item.Id));
    }

    [Test]
    public void GetWorkItem_WhenNotFound_ThrowsMcpException()
    {
        var id = Guid.NewGuid();
        _workItems.Setup(w => w.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((WorkItem?)null);

        Assert.ThrowsAsync<McpException>(() => _tools.GetWorkItem(id, CancellationToken.None));
    }

    [Test]
    public async Task ListWorkItemChildren_DelegatesToServiceAndMapsResults()
    {
        var parentId = Guid.NewGuid();
        var child = MakeWorkItem();
        _workItems.Setup(w => w.GetChildrenAsync(parentId, It.IsAny<CancellationToken>())).ReturnsAsync([child]);

        var result = await _tools.ListWorkItemChildren(parentId, CancellationToken.None);

        Assert.That(result.Single().Id, Is.EqualTo(child.Id));
    }

    [Test]
    public async Task CreateWorkItem_DelegatesToServiceWithGivenArguments()
    {
        var parentId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var afterId = Guid.NewGuid();
        var layerId = Guid.NewGuid();
        var created = MakeWorkItem();
        _workItems
            .Setup(w => w.CreateAsync(
                "Title", "Description", parentId, statusId, afterId, layerId, WorkItemPriority.High, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _tools.CreateWorkItem(
            "Title", statusId, "Description", parentId, afterId, layerId, WorkItemPriority.High, CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo(created.Id));
    }

    [Test]
    public void CreateWorkItem_WhenParentNotFound_ThrowsMcpExceptionWithSameMessage()
    {
        var statusId = Guid.NewGuid();
        var missingParentId = Guid.NewGuid();
        var domainException = new EntityNotFoundException(nameof(WorkItem), missingParentId);
        _workItems
            .Setup(w => w.CreateAsync(
                "Title", null, missingParentId, statusId, null, null, WorkItemPriority.Medium, It.IsAny<CancellationToken>()))
            .ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.CreateWorkItem("Title", statusId, parentId: missingParentId, ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task ChangeWorkItemStatus_DelegatesToService()
    {
        var item = MakeWorkItem();
        var statusId = Guid.NewGuid();
        _workItems.Setup(w => w.ChangeStatusAsync(item.Id, statusId, null, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _tools.ChangeWorkItemStatus(item.Id, statusId, ct: CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo(item.Id));
        _workItems.VerifyAll();
    }

    [Test]
    public void ReparentWorkItem_WhenWouldCreateCycle_ThrowsMcpExceptionWithSameMessage()
    {
        var id = Guid.NewGuid();
        var proposedParentId = Guid.NewGuid();
        var domainException = new CyclicParentException(id, proposedParentId);
        _workItems
            .Setup(w => w.ReparentAsync(id, proposedParentId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.ReparentWorkItem(id, proposedParentId, ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public void DeleteWorkItem_WhenHasChildrenAndCascadeFalse_ThrowsMcpExceptionWithSameMessage()
    {
        var id = Guid.NewGuid();
        var domainException = new WorkItemHasChildrenException(id);
        _workItems.Setup(w => w.DeleteAsync(id, false, It.IsAny<CancellationToken>())).ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() => _tools.DeleteWorkItem(id, ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task SetWorkItemTags_DelegatesToService()
    {
        var item = MakeWorkItem();
        var tags = new List<string> { "urgent", "needs review" };
        _workItems.Setup(w => w.SetTagsAsync(item.Id, tags, 7u, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _tools.SetWorkItemTags(item.Id, tags, expectedVersion: 7u, ct: CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo(item.Id));
        _workItems.VerifyAll();
    }

    [Test]
    public void SetWorkItemTags_WhenTagIsBlank_ThrowsMcpExceptionWithSameMessage()
    {
        var id = Guid.NewGuid();
        var tags = new List<string> { "  " };
        var domainException = new InvalidWorkItemTagException(id);
        _workItems.Setup(w => w.SetTagsAsync(id, tags, null, It.IsAny<CancellationToken>())).ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.SetWorkItemTags(id, tags, ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task DeleteWorkItem_WithCascadeTrue_DelegatesToService()
    {
        var id = Guid.NewGuid();
        _workItems.Setup(w => w.DeleteAsync(id, true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _tools.DeleteWorkItem(id, cascade: true, ct: CancellationToken.None);

        _workItems.VerifyAll();
    }
}
