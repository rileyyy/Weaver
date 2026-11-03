using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemTools
{
    private const string ExpectedVersionDescription =
        "The work item's version from your last read. If given and the item has changed since, the " +
        "write is rejected instead of overwriting someone else's change.";

    private const string PatchDescription =
        "Only the fields you send change: leave a field out to keep it, or send null to clear it. " +
        "Returns the item with its new version and the list of fields that changed.";

    private readonly IWorkItemService _workItems;
    private readonly HttpCallerContext _caller;

    public WorkItemTools(IWorkItemService workItems, HttpCallerContext caller)
    {
        _workItems = workItems;
        _caller = caller;
    }

    [McpServerTool(Name = "get_work_item", ReadOnly = true)]
    [Description("Gets a single work item by id.")]
    public async Task<WorkItemDto> GetWorkItem(
        [Description("The work item's id.")] Guid id,
        CancellationToken ct)
    {
        var item = await _workItems.GetByIdAsync(id, ct);
        return item is not null ? WorkItemDto.FromEntity(item) : throw new McpException($"Work item {id} was not found.");
    }

    [McpServerTool(Name = "list_work_item_children", ReadOnly = true)]
    [Description("Lists the direct children of a work item (e.g. a swimlane's cards), or top-level " +
        "work items when parentId is omitted (e.g. a board's swimlanes).")]
    public async Task<IReadOnlyList<WorkItemDto>> ListWorkItemChildren(
        [Description("The parent work item's id. Omit to list top-level work items.")] Guid? parentId,
        CancellationToken ct)
    {
        var items = await _workItems.GetChildrenAsync(parentId, ct);
        return items.Select(WorkItemDto.FromEntity).ToList();
    }

    [McpServerTool(Name = "list_all_work_items", ReadOnly = true)]
    [Description("Lists one page of every work item, flat and unscoped, ordered by rank then number. " +
        "Rebuild the tree from each item's parentId. Page by increasing offset until a page comes back " +
        "shorter than limit.")]
    public Task<IReadOnlyList<WorkItemDto>> ListAllWorkItems(
        [Description("How many items to skip. Defaults to 0.")] int offset = 0,
        [Description("Page size, 1 to 1000. Defaults to 500.")] int limit = WorkItemService.DefaultPageSize,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync<IReadOnlyList<WorkItemDto>>(async () =>
        {
            var items = await _workItems.GetAllAsync(offset, limit, ct);
            return items.Select(WorkItemDto.FromEntity).ToList();
        });

    [McpServerTool(Name = "create_work_item", Destructive = false)]
    [Description("Creates a new work item.")]
    public Task<WorkItemDto> CreateWorkItem(
        [Description("Title of the work item.")] string title,
        [Description("Id of the status column to place the item in. Use list_statuses to find valid ids.")] Guid statusId,
        [Description("Longer free-text description. Optional.")] string? description = null,
        [Description("Id of the parent work item this item will become a child of. Omit to create a top-level item.")] Guid? parentId = null,
        [Description("Id of an existing sibling (same parent and status) to place this item immediately after. Omit to place it first.")] Guid? afterId = null,
        [Description("Id of the work item layer, e.g. Project/Goal/Task. Use list_work_item_layers to find valid ids. Optional.")] Guid? layerId = null,
        [Description("Id of the work item priority. Use list_work_item_priorities to find valid ids. Optional.")] Guid? priorityId = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            _caller.UseActorLabel(actorLabel);
            var item = await _workItems.CreateAsync(title, description, parentId, statusId, afterId, layerId, priorityId, ct);
            return WorkItemDto.FromEntity(item);
        });

    [McpServerTool(Name = "change_work_item_status", Destructive = false, Idempotent = true)]
    [Description("Moves a work item to a different status column, without changing its parent. " +
        "Cannot reparent it — see reparent_work_item.")]
    public Task<WorkItemWriteResponse> ChangeWorkItemStatus(
        [Description("The work item's id.")] Guid id,
        [Description("Id of the status column to move the item to.")] Guid statusId,
        [Description("Id of an existing sibling (same parent and status) to place this item immediately after. Omit to place it first.")] Guid? afterId = null,
        [Description(ExpectedVersionDescription)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        TranslateWriteAsync(actorLabel, () => _workItems.ChangeStatusAsync(id, statusId, afterId, expectedVersion, ct));

    [McpServerTool(Name = "reparent_work_item", Destructive = false, Idempotent = true)]
    [Description("Moves a work item to a different parent, keeping its current status. Cannot change " +
        "its status — see change_work_item_status. Rejects a move that would make the item its own ancestor.")]
    public Task<WorkItemWriteResponse> ReparentWorkItem(
        [Description("The work item's id.")] Guid id,
        [Description("Id of the new parent work item. Omit to make it a top-level item.")] Guid? parentId = null,
        [Description("Id of an existing sibling under the new parent (same status) to place this item immediately after. Omit to place it first.")] Guid? afterId = null,
        [Description(ExpectedVersionDescription)] uint? expectedVersion = null,
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
        [Description(ExpectedVersionDescription)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        TranslateWriteAsync(actorLabel, () => _workItems.RescheduleAsync(
            id,
            context.Supplied(nameof(startDate), startDate),
            context.Supplied(nameof(endDate), endDate),
            expectedVersion,
            ct));

    [McpServerTool(Name = "update_work_item_details", Destructive = false, Idempotent = true)]
    [Description("Changes a work item's title, description, layer, priority and/or When. Independent of " +
        "status, parent, schedule, and assignee — use their own tools for those. " + PatchDescription)]
    public Task<WorkItemWriteResponse> UpdateWorkItemDetails(
        RequestContext<CallToolRequestParams> context,
        [Description("The work item's id.")] Guid id,
        [Description("New title. Can't be cleared.")] string? title = null,
        [Description("New description, or null to clear it.")] string? description = null,
        [Description("Id of the work item layer, or null to clear it. Use list_work_item_layers to find valid ids.")] Guid? layerId = null,
        [Description("Id of the work item priority, or null to clear it. Use list_work_item_priorities to find valid ids.")] Guid? priorityId = null,
        [Description("Free-text phrase for when the item should happen (e.g. \"after drywall\"), or null to clear it. " +
            "It stays unresolved until the item has dates; set those with reschedule_work_item and keep the phrase.")] string? when = null,
        [Description(ExpectedVersionDescription)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default)
    {
        var patch = new WorkItemDetailsPatch
        {
            Title = context.Supplied(nameof(title), title!),
            Description = context.Supplied(nameof(description), description),
            LayerId = context.Supplied(nameof(layerId), layerId),
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
        [Description(ExpectedVersionDescription)] uint? expectedVersion = null,
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
        [Description(ExpectedVersionDescription)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default)
    {
        var supplied = context.Supplied(nameof(tags), tags);
        var patch = supplied.HasValue
            ? new Optional<IReadOnlyList<string>>(supplied.Value ?? [])
            : Optional<IReadOnlyList<string>>.Unset;
        return TranslateWriteAsync(actorLabel, () => _workItems.SetTagsAsync(id, patch, expectedVersion, ct));
    }

    [McpServerTool(Name = "delete_work_item", Destructive = true)]
    [Description("Deletes a work item. If it has children, cascade must be true or the delete is " +
        "rejected — a subtree is never silently dropped. Links to any deleted item are removed. " +
        "Rejected if the item or any descendant is a board's scope.")]
    public Task DeleteWorkItem(
        [Description("The work item's id.")] Guid id,
        [Description("Must be true to delete a work item that has children; this also deletes the entire subtree.")] bool cascade = false,
        [Description(ExpectedVersionDescription)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(() =>
        {
            _caller.UseActorLabel(actorLabel);
            return _workItems.DeleteAsync(id, cascade, expectedVersion, ct);
        });

    private Task<WorkItemWriteResponse> TranslateWriteAsync(string? actorLabel, Func<Task<WorkItemWriteResult>> write) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            _caller.UseActorLabel(actorLabel);
            return WorkItemWriteResponse.FromResult(await write());
        });
}
