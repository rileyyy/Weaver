using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemTools(IWorkItemService workItems, WorkItemDtoMapper dtos, HttpCallerContext caller)
{
    private const string PatchDescription =
        "Only the fields you send change: leave a field out to keep it, or send null to clear it. " +
        "Returns the item with its new version and the list of fields that changed.";

    private readonly IWorkItemService _workItems = workItems;
    private readonly WorkItemDtoMapper _dtos = dtos;
    private readonly HttpCallerContext _caller = caller;

    [McpServerTool(Name = "get_work_item", ReadOnly = true)]
    [Description("Gets a single work item in full detail, by id or by Number (the #N people use). Give exactly " +
        "one of id and number. List and search tools return summaries; use this for the rest of an item.")]
    public async Task<WorkItemDto> GetWorkItem(
        [Description("The work item's id.")] Guid? id = null,
        [Description("The work item's Number, without the # (123 for #123).")] int? number = null,
        CancellationToken ct = default)
    {
        var item = await FindByIdOrNumberAsync(id, number, ct);
        return await _dtos.ToDtoAsync(item, ct);
    }

    [McpServerTool(Name = "list_work_item_children", ReadOnly = true)]
    [Description("Lists the direct children of a work item (e.g. a swimlane's cards), or the top-level " +
        "work items of a Space when parentId is omitted (e.g. a board's swimlanes), in board order. Give " +
        "parentId or spaceId. Each item is a brief summary with names; use get_work_item for full detail.")]
    public Task<IReadOnlyList<WorkItemSummaryDto>> ListWorkItemChildren(
        [Description("The parent work item's id. Omit to list a Space's top-level work items.")] Guid? parentId = null,
        [Description("The Space whose top-level work items to list when parentId is omitted. Use list_spaces to find valid ids.")] Guid? spaceId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
            ToSummaryDtos(await _workItems.GetChildSummariesAsync(parentId, spaceId, ct)));

    [McpServerTool(Name = "list_all_work_items", ReadOnly = true)]
    [Description("Lists one page of every work item in a Space (or every Space), flat, ordered by rank " +
        "then number. Each item is a brief summary with names; use get_work_item for full detail. Rebuild " +
        "the tree from each item's parentId, which top-level items don't have. Page by increasing offset " +
        "until a page comes back shorter than limit.")]
    public Task<IReadOnlyList<WorkItemSummaryDto>> ListAllWorkItems(
        [Description("How many items to skip. Defaults to 0.")] int offset = 0,
        [Description("Page size, 1 to 1000. Defaults to 500.")] int limit = WorkItemService.DefaultPageSize,
        [Description("Only list items in this Space. Omit for every Space.")] Guid? spaceId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
            ToSummaryDtos(await _workItems.GetAllSummariesAsync(offset, limit, spaceId, ct)));

    [McpServerTool(Name = "create_work_item", Destructive = false)]
    [Description("Creates a new work item.")]
    public Task<WorkItemDto> CreateWorkItem(
        [Description("Title of the work item.")] string title,
        [Description("Id of the status column to place the item in: one of its Space's, from list_statuses.")] Guid statusId,
        [Description("Longer free-text description. Optional.")] string? description = null,
        [Description("Id of the parent work item this item will become a child of; the item joins the parent's Space. Omit to create a top-level item, which then needs spaceId.")] Guid? parentId = null,
        [Description("Id of an existing sibling (same parent and status) to place this item immediately after. Omit to place it first.")] Guid? afterId = null,
        [Description("Id of the Type (e.g. Project/Goal/Task): one of its Space's, from list_work_item_types. Optional.")] Guid? typeId = null,
        [Description("Id of the work item priority: one of its Space's, from list_work_item_priorities. Optional.")] Guid? priorityId = null,
        [Description("Id of the Space to create a top-level item in. Use list_spaces to find valid ids. Required without parentId; with one, omit it or send the parent's.")] Guid? spaceId = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            _caller.UseActorLabel(actorLabel);
            var item = await _workItems.CreateAsync(title, description, parentId, statusId, afterId, typeId, priorityId, spaceId, ct);
            return await _dtos.ToDtoAsync(item, ct);
        });

    [McpServerTool(Name = "change_work_item_status", Destructive = false, Idempotent = true)]
    [Description("Moves a work item to a different status column, without changing its parent. " +
        "Cannot reparent it — see reparent_work_item.")]
    public Task<WorkItemWriteResponse> ChangeWorkItemStatus(
        [Description("The work item's id.")] Guid id,
        [Description("Id of the status column to move the item to: one of the item's own Space's, from list_statuses.")] Guid statusId,
        [Description("Id of an existing sibling (same parent and status) to place this item immediately after. Omit to place it first.")] Guid? afterId = null,
        [Description(ExpectedVersionArgument.Description)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        TranslateWriteAsync(actorLabel, () => _workItems.ChangeStatusAsync(id, statusId, afterId, expectedVersion, ct));

    [McpServerTool(Name = "reparent_work_item", Destructive = false, Idempotent = true)]
    [Description("Moves a work item to a different parent, keeping its current status and Space. Cannot change " +
        "its status — see change_work_item_status. Rejects a move that would make the item its own ancestor, " +
        "or put it under an item in another Space.")]
    public Task<WorkItemWriteResponse> ReparentWorkItem(
        [Description("The work item's id.")] Guid id,
        [Description("Id of the new parent work item, in the same Space. Omit to make it a top-level item of its Space.")] Guid? parentId = null,
        [Description("Id of an existing sibling under the new parent (same status) to place this item immediately after. Omit to place it first.")] Guid? afterId = null,
        [Description(ExpectedVersionArgument.Description)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        TranslateWriteAsync(actorLabel, () => _workItems.ReparentAsync(id, parentId, afterId, expectedVersion, ct));

    [McpServerTool(Name = "reschedule_work_item", Destructive = false, Idempotent = true)]
    [Description("Changes a work item's scheduled start and/or end date, independently of status and " +
        "parent. " + PatchDescription)]
    public Task<WorkItemWriteResponse> RescheduleWorkItem(
        RequestContext<CallToolRequestParams> context,
        [Description("The work item's id.")] Guid id,
        [Description("New start date (yyyy-MM-dd, a calendar date), or null to clear it.")] DateOnly? startDate = null,
        [Description("New end date (yyyy-MM-dd, a calendar date), or null to clear it.")] DateOnly? endDate = null,
        [Description(ExpectedVersionArgument.Description)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        TranslateWriteAsync(actorLabel, () => _workItems.RescheduleAsync(
            id,
            context.Supplied(nameof(startDate), startDate),
            context.Supplied(nameof(endDate), endDate),
            expectedVersion,
            ct));

    [McpServerTool(Name = "update_work_item_details", Destructive = false, Idempotent = true)]
    [Description("Changes a work item's title, description, type, priority and/or When. Independent of " +
        "status, parent, schedule, and assignee — use their own tools for those. Changing the Type clears the " +
        "item's custom field values. " + PatchDescription)]
    public Task<WorkItemWriteResponse> UpdateWorkItemDetails(
        RequestContext<CallToolRequestParams> context,
        [Description("The work item's id.")] Guid id,
        [Description("New title. Can't be cleared.")] string? title = null,
        [Description("New description, or null to clear it.")] string? description = null,
        [Description("Id of the Type (one of the item's own Space's, from list_work_item_types), or null to clear it.")] Guid? typeId = null,
        [Description("Id of the work item priority (one of the item's own Space's, from list_work_item_priorities), or null to clear it.")] Guid? priorityId = null,
        [Description("Free-text phrase for when the item should happen (e.g. \"after drywall\"), or null to clear it. " +
            "It stays unresolved until the item has dates; set those with reschedule_work_item and keep the phrase.")] string? when = null,
        [Description(ExpectedVersionArgument.Description)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default)
    {
        var patch = new WorkItemDetailsPatch
        {
            Title = context.Supplied(nameof(title), title!),
            Description = context.Supplied(nameof(description), description),
            TypeId = context.Supplied(nameof(typeId), typeId),
            PriorityId = context.Supplied(nameof(priorityId), priorityId),
            When = context.Supplied(nameof(when), when),
        };
        return TranslateWriteAsync(actorLabel, () => _workItems.UpdateDetailsAsync(id, patch, expectedVersion, ct));
    }

    [McpServerTool(Name = "assign_work_item", Destructive = false, Idempotent = true)]
    [Description("Sets or clears who a work item is assigned to, independently of every other field.")]
    public Task<WorkItemWriteResponse> AssignWorkItem(
        [Description("The work item's id.")] Guid id,
        [Description("Id of the user to assign. Omit to unassign.")] Guid? userId = null,
        [Description(ExpectedVersionArgument.Description)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        TranslateWriteAsync(actorLabel, () => _workItems.AssignAsync(id, userId, expectedVersion, ct));

    [McpServerTool(Name = "set_work_item_tags", Destructive = false, Idempotent = true)]
    [Description("Replaces a work item's full tag list, independently of every other field. Tags are " +
        "trimmed and de-duplicated case-insensitively; an empty/blank tag is rejected.")]
    public Task<WorkItemWriteResponse> SetWorkItemTags(
        RequestContext<CallToolRequestParams> context,
        [Description("The work item's id.")] Guid id,
        [Description("The complete new list of tags, replacing the existing list entirely. Null or an empty list clears the tags; omitted leaves them unchanged.")] IReadOnlyList<string>? tags = null,
        [Description(ExpectedVersionArgument.Description)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default)
    {
        var supplied = context.Supplied(nameof(tags), tags);
        var patch = supplied.HasValue
            ? new Optional<IReadOnlyList<string>>(supplied.Value ?? [])
            : Optional<IReadOnlyList<string>>.Unset;
        return TranslateWriteAsync(actorLabel, () => _workItems.SetTagsAsync(id, patch, expectedVersion, ct));
    }

    [McpServerTool(Name = "set_work_item_fields", Destructive = false, Idempotent = true)]
    [Description("Sets a work item's custom field values, independently of every other field. The item's " +
        "Type defines its fields: list_work_item_types gives each field's id, kind and options. Only the " +
        "fields you send change: leave a field out to keep it, or send null to clear it. Changing the item's " +
        "Type clears every value. Returns the item with its new version and the changed fields, named fields.<fieldId>.")]
    public Task<WorkItemWriteResponse> SetWorkItemFields(
        [Description("The work item's id.")] Guid id,
        [Description("Values by field id. By kind: Text and Markdown take a string; Number a number; Currency a " +
            "number with at most 2 decimals; Date \"yyyy-MM-dd\"; Checkbox true or false; SingleSelect one option id; " +
            "MultiSelect a list of option ids; User a user id (from list_users); Url an http(s) address.")]
        IReadOnlyDictionary<Guid, JsonElement> values,
        [Description(ExpectedVersionArgument.Description)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        TranslateWriteAsync(actorLabel, () => _workItems.SetCustomFieldsAsync(id, values, expectedVersion, ct));

    private async Task<WorkItem> FindByIdOrNumberAsync(Guid? id, int? number, CancellationToken ct) =>
        (id, number) switch
        {
            ({ } byId, null) => await _workItems.GetByIdAsync(byId, ct)
                ?? throw new McpException($"Work item {byId} was not found."),
            (null, { } byNumber) => await _workItems.GetByNumberAsync(byNumber, ct)
                ?? throw new McpException($"Work item #{byNumber} was not found."),
            _ => throw new McpException("Give exactly one of id and number."),
        };

    private static IReadOnlyList<WorkItemSummaryDto> ToSummaryDtos(IReadOnlyList<WorkItemSummary> summaries) =>
        summaries.Select(WorkItemSummaryDto.FromSummary).ToList();

    private Task<WorkItemWriteResponse> TranslateWriteAsync(string? actorLabel, Func<Task<WorkItemWriteResult>> write) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            _caller.UseActorLabel(actorLabel);
            return await _dtos.ToWriteResponseAsync(await write());
        });
}
