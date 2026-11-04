using Microsoft.AspNetCore.Http;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Auth;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

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
        _tools = new WorkItemTools(_workItems.Object, new HttpCallerContext(new HttpContextAccessor()));
    }

    private static WorkItem MakeWorkItem() => WorkItem.Create(Guid.NewGuid(), "A work item", null, Guid.NewGuid(), rank: 1.0);

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
        _workItems.Setup(w => w.GetChildrenAsync(parentId, null, It.IsAny<CancellationToken>())).ReturnsAsync([child]);

        var result = await _tools.ListWorkItemChildren(parentId, ct: CancellationToken.None);

        Assert.That(result.Single().Id, Is.EqualTo(child.Id));
    }

    [Test]
    public async Task ListWorkItemChildren_WithOnlyASpace_ListsItsTopLevelItems()
    {
        var spaceId = Guid.NewGuid();
        var top = MakeWorkItem();
        _workItems.Setup(w => w.GetChildrenAsync(null, spaceId, It.IsAny<CancellationToken>())).ReturnsAsync([top]);

        var result = await _tools.ListWorkItemChildren(spaceId: spaceId, ct: CancellationToken.None);

        Assert.That(result.Single().Id, Is.EqualTo(top.Id));
    }

    [Test]
    public void ListWorkItemChildren_WithNeitherParentNorSpace_ThrowsMcpExceptionWithTheServiceMessage()
    {
        _workItems.Setup(w => w.GetChildrenAsync(null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("Give a parent, or a Space."));

        var ex = Assert.ThrowsAsync<McpException>(() => _tools.ListWorkItemChildren());

        Assert.That(ex!.Message, Is.EqualTo("Give a parent, or a Space."));
    }

    [Test]
    public async Task ListAllWorkItems_DelegatesTheRequestedPageAndMapsResults()
    {
        var items = new[] { MakeWorkItem(), MakeWorkItem() };
        var spaceId = Guid.NewGuid();
        _workItems.Setup(w => w.GetAllAsync(500, 2, spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(items);

        var result = await _tools.ListAllWorkItems(offset: 500, limit: 2, spaceId, CancellationToken.None);

        Assert.That(result.Select(d => d.Id), Is.EqualTo(items.Select(i => i.Id)));
    }

    [Test]
    public async Task ListAllWorkItems_DefaultsToTheFirstPageOfTheDefaultSize()
    {
        _workItems
            .Setup(w => w.GetAllAsync(0, WorkItemService.DefaultPageSize, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _tools.ListAllWorkItems();

        Assert.That(result, Is.Empty);
        _workItems.VerifyAll();
    }

    [Test]
    public void ListAllWorkItems_WhenThePageIsInvalid_ThrowsMcpExceptionWithTheServiceMessage()
    {
        _workItems
            .Setup(w => w.GetAllAsync(0, 0, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("Limit must be between 1 and 1000."));

        var ex = Assert.ThrowsAsync<McpException>(() => _tools.ListAllWorkItems(offset: 0, limit: 0));

        Assert.That(ex!.Message, Is.EqualTo("Limit must be between 1 and 1000."));
    }

    [Test]
    public async Task CreateWorkItem_DelegatesToServiceWithGivenArguments()
    {
        var parentId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var afterId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var priorityId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var created = MakeWorkItem();
        _workItems
            .Setup(w => w.CreateAsync(
                "Title", "Description", parentId, statusId, afterId, typeId, priorityId, spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _tools.CreateWorkItem(
            "Title", statusId, "Description", parentId, afterId, typeId, priorityId, spaceId, ct: CancellationToken.None);

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
                "Title", null, missingParentId, statusId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.CreateWorkItem("Title", statusId, parentId: missingParentId, ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task ChangeWorkItemStatus_PassesTheExpectedVersion_AndReturnsTheChangedFields()
    {
        var item = MakeWorkItem();
        var statusId = Guid.NewGuid();
        _workItems.Setup(w => w.ChangeStatusAsync(item.Id, statusId, null, 7u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.StatusId]));

        var result = await _tools.ChangeWorkItemStatus(item.Id, statusId, expectedVersion: 7u, ct: CancellationToken.None);

        Assert.That(result.Item.Id, Is.EqualTo(item.Id));
        Assert.That(result.ChangedFields, Is.EqualTo(new[] { WorkItemField.StatusId }));
        _workItems.VerifyAll();
    }

    [Test]
    public void ChangeWorkItemStatus_WhenTheVersionIsStale_ThrowsMcpExceptionWithSameMessage()
    {
        var id = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var domainException = new WorkItemVersionConflictException(id);
        _workItems.Setup(w => w.ChangeStatusAsync(id, statusId, null, 7u, It.IsAny<CancellationToken>()))
            .ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.ChangeWorkItemStatus(id, statusId, expectedVersion: 7u, ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public void ReparentWorkItem_WhenWouldCreateCycle_ThrowsMcpExceptionWithSameMessage()
    {
        var id = Guid.NewGuid();
        var proposedParentId = Guid.NewGuid();
        var domainException = new CyclicParentException(id, proposedParentId);
        _workItems
            .Setup(w => w.ReparentAsync(id, proposedParentId, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.ReparentWorkItem(id, proposedParentId, ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task UpdateWorkItemDetails_WithOnlyATitleSent_LeavesTheOtherFieldsOutOfThePatch()
    {
        var item = MakeWorkItem();
        var expectedPatch = new WorkItemDetailsPatch { Title = "Renamed" };
        _workItems.Setup(w => w.UpdateDetailsAsync(item.Id, expectedPatch, 7u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.Title]));

        await _tools.UpdateWorkItemDetails(
            CallWith("id", "title", "expectedVersion"), item.Id, title: "Renamed", expectedVersion: 7u, ct: CancellationToken.None);

        _workItems.VerifyAll();
    }

    [Test]
    public async Task UpdateWorkItemDetails_WithANullSent_ClearsThatField()
    {
        var item = MakeWorkItem();
        var expectedPatch = new WorkItemDetailsPatch { Description = null, TypeId = null };
        _workItems.Setup(w => w.UpdateDetailsAsync(item.Id, expectedPatch, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.Description]));

        await _tools.UpdateWorkItemDetails(
            CallWith("id", "description", "typeId"), item.Id, description: null, typeId: null, ct: CancellationToken.None);

        _workItems.VerifyAll();
    }

    [Test]
    public async Task UpdateWorkItemDetails_WithAWhenSent_PatchesOnlyTheWhen()
    {
        var item = MakeWorkItem();
        var expectedPatch = new WorkItemDetailsPatch { When = "before the Christmas market" };
        _workItems.Setup(w => w.UpdateDetailsAsync(item.Id, expectedPatch, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.When]));

        await _tools.UpdateWorkItemDetails(
            CallWith("id", "when"), item.Id, when: "before the Christmas market", ct: CancellationToken.None);

        _workItems.VerifyAll();
    }

    [Test]
    public async Task RescheduleWorkItem_WithOnlyAnEndDateSent_KeepsTheStartDate()
    {
        var item = MakeWorkItem();
        var end = new DateOnly(2026, 9, 25);
        _workItems.Setup(w => w.RescheduleAsync(
                item.Id, Optional<DateOnly?>.Unset, new Optional<DateOnly?>(end), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.EndDate]));

        await _tools.RescheduleWorkItem(CallWith("id", "endDate"), item.Id, endDate: end, ct: CancellationToken.None);

        _workItems.VerifyAll();
    }

    [Test]
    public async Task SetWorkItemTags_DelegatesToService()
    {
        var item = MakeWorkItem();
        var tags = new List<string> { "urgent", "needs review" };
        _workItems.Setup(w => w.SetTagsAsync(
                item.Id,
                It.Is<Optional<IReadOnlyList<string>>>(t => t.HasValue && t.Value.SequenceEqual(tags)),
                7u,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.Tags]));

        var result = await _tools.SetWorkItemTags(
            CallWith("id", "tags", "expectedVersion"), item.Id, tags, expectedVersion: 7u, ct: CancellationToken.None);

        Assert.That(result.Item.Id, Is.EqualTo(item.Id));
        _workItems.VerifyAll();
    }

    [Test]
    public async Task SetWorkItemFields_DelegatesTheValuesToTheService()
    {
        var item = MakeWorkItem();
        var fieldId = Guid.NewGuid();
        var values = new Dictionary<Guid, JsonElement> { [fieldId] = JsonSerializer.SerializeToElement("Oak") };
        _workItems.Setup(w => w.SetCustomFieldsAsync(item.Id, values, 7u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.CustomField(fieldId)]));

        var result = await _tools.SetWorkItemFields(item.Id, values, expectedVersion: 7u, ct: CancellationToken.None);

        Assert.That(result.ChangedFields, Is.EqualTo(new[] { $"fields.{fieldId}" }));
    }

    [Test]
    public void SetWorkItemFields_WithAValueTheFieldRejects_IsAToolError()
    {
        var id = Guid.NewGuid();
        _workItems.Setup(w => w.SetCustomFieldsAsync(
                id, It.IsAny<IReadOnlyDictionary<Guid, JsonElement>>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("Budget takes a number."));

        var error = Assert.ThrowsAsync<McpException>(() =>
            _tools.SetWorkItemFields(id, new Dictionary<Guid, JsonElement>(), ct: CancellationToken.None));

        Assert.That(error!.Message, Does.Contain("Budget takes a number."));
    }

    [Test]
    public async Task SetWorkItemTags_WithNullSent_ClearsTheTags()
    {
        var item = MakeWorkItem();
        _workItems.Setup(w => w.SetTagsAsync(
                item.Id,
                It.Is<Optional<IReadOnlyList<string>>>(t => t.HasValue && t.Value.Count == 0),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, []));

        await _tools.SetWorkItemTags(CallWith("id", "tags"), item.Id, tags: null, ct: CancellationToken.None);

        _workItems.VerifyAll();
    }

    [Test]
    public void SetWorkItemTags_WhenTagIsBlank_ThrowsMcpExceptionWithSameMessage()
    {
        var id = Guid.NewGuid();
        var tags = new List<string> { "  " };
        var domainException = new InvalidWorkItemTagException(id);
        _workItems.Setup(w => w.SetTagsAsync(
                id, It.IsAny<Optional<IReadOnlyList<string>>>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.SetWorkItemTags(CallWith("id", "tags"), id, tags, ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    /// <summary>A tools/call request that sent exactly the named arguments.</summary>
    private static RequestContext<CallToolRequestParams> CallWith(params string[] argumentNames) =>
        new(
            new Mock<McpServer>().Object,
            new JsonRpcRequest { Method = RequestMethods.ToolsCall },
            new CallToolRequestParams
            {
                Name = "work_item_tool",
                Arguments = argumentNames.ToDictionary(name => name, _ => JsonDocument.Parse("null").RootElement),
            });
}
