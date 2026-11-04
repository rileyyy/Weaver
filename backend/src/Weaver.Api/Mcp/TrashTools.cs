using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

/// <summary>
/// Deleting moves work items to the Trash; agents can list it and restore from it, but not
/// purge it (ADR-0007). A read-only key can only list.
/// </summary>
[McpServerToolType]
public class TrashTools(IWorkItemTrashService trash, HttpCallerContext caller)
{
    private readonly IWorkItemTrashService _trash = trash;
    private readonly HttpCallerContext _caller = caller;

    [McpServerTool(Name = "delete_work_item", Destructive = true)]
    [Description("Deletes a work item by moving it, and its whole subtree, to the Trash, where it can be " +
        "restored with restore_work_item. If it has children, cascade must be true or the delete is " +
        "rejected. Its links and comments are kept, but hidden while it's in the Trash.")]
    public Task DeleteWorkItem(
        [Description("The work item's id.")] Guid id,
        [Description("Must be true to delete a work item that has children; this also deletes the entire subtree.")] bool cascade = false,
        [Description(ExpectedVersionArgument.Description)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(() =>
        {
            _caller.UseActorLabel(actorLabel);
            return _trash.MoveToTrashAsync(id, cascade, expectedVersion, ct);
        });

    [McpServerTool(Name = "list_trash", ReadOnly = true)]
    [Description("Lists the Trash, newest first: one entry per delete, with the deleted item as a summary, " +
        "when and by whom it was deleted, and itemCount, the number of items (it and its descendants) that " +
        "restoring it brings back.")]
    public Task<IReadOnlyList<TrashedWorkItemDto>> ListTrash(
        [Description("Only this Space's Trash. Use list_spaces to find valid ids. Omit for every Space.")] Guid? spaceId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var trash = await _trash.ListAsync(spaceId, ct);
            return (IReadOnlyList<TrashedWorkItemDto>)trash.Select(TrashedWorkItemDto.FromModel).ToList();
        });

    [McpServerTool(Name = "restore_work_item", Destructive = false)]
    [Description("Restores a deleted work item, and everything deleted with it, from the Trash. It goes back " +
        "under its parent if that still exists, otherwise under its nearest ancestor that does, otherwise to " +
        "the top level of its Space. Use an id from list_trash; an item deleted along with its parent is " +
        "restored by restoring that parent. Returns the restored item.")]
    public Task<WorkItemDto> RestoreWorkItem(
        [Description("The deleted work item's id, from list_trash.")] Guid id,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            _caller.UseActorLabel(actorLabel);
            return WorkItemDto.FromEntity(await _trash.RestoreAsync(id, ct));
        });
}
