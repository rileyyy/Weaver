using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Domain;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemTools
{
    private readonly IWorkItemService _workItems;

    public WorkItemTools(IWorkItemService workItems)
    {
        _workItems = workItems;
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

    [McpServerTool(Name = "create_work_item", Destructive = false)]
    [Description("Creates a new work item.")]
    public Task<WorkItemDto> CreateWorkItem(
        [Description("Title of the work item.")] string title,
        [Description("Id of the status column to place the item in. Use list_statuses to find valid ids.")] Guid statusId,
        [Description("Longer free-text description. Optional.")] string? description = null,
        [Description("Id of the parent work item this item will become a child of. Omit to create a top-level item.")] Guid? parentId = null,
        [Description("Id of an existing sibling (same parent and status) to place this item immediately after. Omit to place it first.")] Guid? afterId = null,
        [Description("Id of the work item layer, e.g. Project/Goal/Task. Use list_work_item_layers to find valid ids. Optional.")] Guid? layerId = null,
        [Description("Priority of the work item. Defaults to Medium.")] WorkItemPriority priority = WorkItemPriority.Medium,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var item = await _workItems.CreateAsync(title, description, parentId, statusId, afterId, layerId, priority, ct);
            return WorkItemDto.FromEntity(item);
        });

    [McpServerTool(Name = "change_work_item_status", Destructive = false, Idempotent = true)]
    [Description("Moves a work item to a different status column, without changing its parent. " +
        "Cannot reparent it — see reparent_work_item.")]
    public Task<WorkItemDto> ChangeWorkItemStatus(
        [Description("The work item's id.")] Guid id,
        [Description("Id of the status column to move the item to.")] Guid statusId,
        [Description("Id of an existing sibling (same parent and status) to place this item immediately after. Omit to place it first.")] Guid? afterId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var item = await _workItems.ChangeStatusAsync(id, statusId, afterId, ct);
            return WorkItemDto.FromEntity(item);
        });

    [McpServerTool(Name = "reparent_work_item", Destructive = false, Idempotent = true)]
    [Description("Moves a work item to a different parent, keeping its current status. Cannot change " +
        "its status — see change_work_item_status. Rejects a move that would make the item its own ancestor.")]
    public Task<WorkItemDto> ReparentWorkItem(
        [Description("The work item's id.")] Guid id,
        [Description("Id of the new parent work item. Omit to make it a top-level item.")] Guid? parentId = null,
        [Description("Id of an existing sibling under the new parent (same status) to place this item immediately after. Omit to place it first.")] Guid? afterId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var item = await _workItems.ReparentAsync(id, parentId, afterId, ct);
            return WorkItemDto.FromEntity(item);
        });

    [McpServerTool(Name = "reschedule_work_item", Destructive = false, Idempotent = true)]
    [Description("Sets a work item's scheduled start/end dates, independently of status and parent. " +
        "Either may be omitted to leave that side open-ended.")]
    public Task<WorkItemDto> RescheduleWorkItem(
        [Description("The work item's id.")] Guid id,
        [Description("New start date. Omit to leave it open-ended on this side.")] DateTimeOffset? startDate = null,
        [Description("New end date. Omit to leave it open-ended on this side.")] DateTimeOffset? endDate = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var item = await _workItems.RescheduleAsync(id, startDate, endDate, ct);
            return WorkItemDto.FromEntity(item);
        });

    [McpServerTool(Name = "update_work_item_details", Destructive = false, Idempotent = true)]
    [Description("Updates a work item's title, description, layer, and priority. Independent of " +
        "status, parent, schedule, and assignee — use their own tools for those.")]
    public Task<WorkItemDto> UpdateWorkItemDetails(
        [Description("The work item's id.")] Guid id,
        [Description("New title.")] string title,
        [Description("New description. Optional.")] string? description = null,
        [Description("Id of the work item layer. Omit to clear it.")] Guid? layerId = null,
        [Description("New priority. Defaults to Medium.")] WorkItemPriority priority = WorkItemPriority.Medium,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var item = await _workItems.UpdateDetailsAsync(id, title, description, layerId, priority, ct);
            return WorkItemDto.FromEntity(item);
        });

    [McpServerTool(Name = "assign_work_item", Destructive = false, Idempotent = true)]
    [Description("Sets or clears who a work item is assigned to, independently of every other field.")]
    public Task<WorkItemDto> AssignWorkItem(
        [Description("The work item's id.")] Guid id,
        [Description("Id of the user to assign. Omit to unassign.")] Guid? userId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var item = await _workItems.AssignAsync(id, userId, ct);
            return WorkItemDto.FromEntity(item);
        });

    [McpServerTool(Name = "delete_work_item", Destructive = true)]
    [Description("Deletes a work item. If it has children, cascade must be true or the delete is " +
        "rejected — a subtree is never silently dropped.")]
    public Task DeleteWorkItem(
        [Description("The work item's id.")] Guid id,
        [Description("Must be true to delete a work item that has children; this also deletes the entire subtree.")] bool cascade = false,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(() => _workItems.DeleteAsync(id, cascade, ct));
}
