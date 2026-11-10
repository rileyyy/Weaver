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
    private Mock<IWorkItemRollUpService> _rollUps = null!;
    private WorkItemTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _workItems = new Mock<IWorkItemService>(MockBehavior.Strict);
        _rollUps = new Mock<IWorkItemRollUpService>(MockBehavior.Strict);
        _rollUps.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _tools = new WorkItemTools(
            _workItems.Object,
            _rollUps.Object,
            new WorkItemDtoMapper(new FakeWorkItemBlockers(), new FakeWorkItemNameLookup()),
            new HttpCallerContext(new HttpContextAccessor()));
    }

    private static WorkItem MakeWorkItem() => TestWorkItems.Create("A work item");

    private static WorkItemSummary MakeSummary(Guid? parentId = null) =>
        new(Guid.NewGuid(), 7, "A work item", Guid.NewGuid(), "Home", parentId, "To Do", "Task", null, null, "alice", 2);

    [Test]
    public async Task GetWorkItem_ById_WhenFound_ReturnsDto()
    {
        var item = MakeWorkItem();
        _workItems.Setup(w => w.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _tools.GetWorkItem(item.Id, ct: CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo(item.Id));
    }

    [Test]
    public async Task GetWorkItem_IncludesTheRolledUpTotals()
    {
        var item = MakeWorkItem();
        var hours = CustomFieldDefinition.Create(Guid.NewGuid(), "Hours", CustomFieldKind.Number, 0, rollsUp: true);
        _workItems.Setup(w => w.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _rollUps.Setup(r => r.GetAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync([new RollUpTotal(hours, 7.5m)]);

        var result = await _tools.GetWorkItem(item.Id, ct: CancellationToken.None);

        Assert.That(
            result.RollUps,
            Is.EqualTo(new[] { new RollUpDto(hours.Id, "Hours", hours.TypeId, CustomFieldKind.Number, 7.5m) }));
    }

    [Test]
    public void GetWorkItem_ById_WhenNotFound_ThrowsMcpException()
    {
        var id = Guid.NewGuid();
        _workItems.Setup(w => w.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((WorkItem?)null);

        Assert.ThrowsAsync<McpException>(() => _tools.GetWorkItem(id, ct: CancellationToken.None));
    }

    [Test]
    public async Task GetWorkItem_ByNumber_WhenFound_ReturnsDto()
    {
        var item = MakeWorkItem();
        _workItems.Setup(w => w.GetByNumberAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _tools.GetWorkItem(number: 123, ct: CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo(item.Id));
    }

    [Test]
    public void GetWorkItem_ByNumber_WhenNotFound_NamesTheNumber()
    {
        _workItems.Setup(w => w.GetByNumberAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync((WorkItem?)null);

        var ex = Assert.ThrowsAsync<McpException>(() => _tools.GetWorkItem(number: 123, ct: CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("#123"));
    }

    [Test]
    public void GetWorkItem_WithNeitherIdNorNumber_ThrowsMcpException()
    {
        var ex = Assert.ThrowsAsync<McpException>(() => _tools.GetWorkItem(ct: CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("id").And.Contain("number"));
    }

    [Test]
    public void GetWorkItem_WithBothIdAndNumber_ThrowsMcpException()
    {
        var ex = Assert.ThrowsAsync<McpException>(() =>
            _tools.GetWorkItem(Guid.NewGuid(), 123, CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("id").And.Contain("number"));
    }

    [Test]
    public async Task ListWorkItemChildren_ReturnsTheServicesSummaries()
    {
        var parentId = Guid.NewGuid();
        var child = MakeSummary(parentId);
        _workItems.Setup(w => w.GetChildSummariesAsync(parentId, null, It.IsAny<CancellationToken>())).ReturnsAsync([child]);

        var result = await _tools.ListWorkItemChildren(parentId, ct: CancellationToken.None);

        Assert.That(result.Single(), Is.EqualTo(WorkItemSummaryDto.FromSummary(child)));
    }

    [Test]
    public async Task ListWorkItemChildren_WithOnlyASpace_ListsItsTopLevelItems()
    {
        var spaceId = Guid.NewGuid();
        var top = MakeSummary();
        _workItems.Setup(w => w.GetChildSummariesAsync(null, spaceId, It.IsAny<CancellationToken>())).ReturnsAsync([top]);

        var result = await _tools.ListWorkItemChildren(spaceId: spaceId, ct: CancellationToken.None);

        Assert.That(result.Single().Id, Is.EqualTo(top.Id));
    }

    [Test]
    public void ListWorkItemChildren_WithNeitherParentNorSpace_ThrowsMcpExceptionWithTheServiceMessage()
    {
        _workItems.Setup(w => w.GetChildSummariesAsync(null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("Give a parent, or a Space."));

        var ex = Assert.ThrowsAsync<McpException>(() => _tools.ListWorkItemChildren());

        Assert.That(ex!.Message, Is.EqualTo("Give a parent, or a Space."));
    }

    [Test]
    public async Task ListAllWorkItems_ReturnsTheRequestedPageOfSummaries()
    {
        var parentId = Guid.NewGuid();
        var summaries = new[] { MakeSummary(), MakeSummary(parentId) };
        var spaceId = Guid.NewGuid();
        _workItems.Setup(w => w.GetAllSummariesAsync(500, 2, spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(summaries);

        var result = await _tools.ListAllWorkItems(offset: 500, limit: 2, spaceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(d => d.Id), Is.EqualTo(summaries.Select(s => s.Id)));
            Assert.That(result[1].ParentId, Is.EqualTo(parentId));
            Assert.That(result[1].StatusName, Is.EqualTo("To Do"));
            Assert.That(result[1].AssigneeName, Is.EqualTo("alice"));
            Assert.That(result[1].ChildCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ListAllWorkItems_DefaultsToTheFirstPageOfTheDefaultSize()
    {
        _workItems
            .Setup(w => w.GetAllSummariesAsync(0, WorkItemService.DefaultPageSize, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _tools.ListAllWorkItems();

        Assert.That(result, Is.Empty);
        _workItems.VerifyAll();
    }

    [Test]
    public void ListAllWorkItems_WhenThePageIsInvalid_ThrowsMcpExceptionWithTheServiceMessage()
    {
        _workItems
            .Setup(w => w.GetAllSummariesAsync(0, 0, null, It.IsAny<CancellationToken>()))
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
