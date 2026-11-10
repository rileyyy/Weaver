using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

/// <summary>
/// Moving a work item into another Space (ADR-0001). A read-only key can plan a move but not
/// make one.
/// </summary>
[McpServerToolType]
public class WorkItemSpaceMoveTools(IWorkItemSpaceMoveService moves, WorkItemDtoMapper dtos, HttpCallerContext caller)
{
    private const string DefaultMappingDescription =
        "A status without a mapping goes to the target status with the same name (ignoring case), else the " +
        "target Space's first status in the same category, else its first status. A Type or priority without " +
        "a mapping goes to the one with the same name, else it's cleared. Custom field values are always " +
        "cleared, since the target's Types have their own fields; history keeps them.";

    private readonly IWorkItemSpaceMoveService _moves = moves;
    private readonly WorkItemDtoMapper _dtos = dtos;
    private readonly HttpCallerContext _caller = caller;

    [McpServerTool(Name = "plan_work_item_space_move", ReadOnly = true)]
    [Description("Shows what move_work_item_to_space would do, without changing anything: how many items move " +
        "(the item and its descendants, trashedItemCount of them in the Trash, which move too), how many custom field values are cleared, and for each status, Type and " +
        "priority they use, its default in the target Space (targetId absent: cleared). " + DefaultMappingDescription)]
    public Task<SpaceMovePlanDto> PlanWorkItemSpaceMove(
        [Description("The work item's id.")] Guid id,
        [Description("The Space to move it to. Use list_spaces to find valid ids.")] Guid targetSpaceId,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
            SpaceMovePlanDto.FromModel(await _moves.PlanAsync(id, targetSpaceId, ct)));

    [McpServerTool(Name = "move_work_item_to_space", Destructive = true)]
    [Description("Moves a work item, with its whole subtree, into another Space in one step; reparent_work_item " +
        "can't cross Spaces. Each item takes the target Space's statuses, Types and priorities: map them " +
        "yourself, or leave them to the defaults. " + DefaultMappingDescription + " Links, comments, attachments, tags, dates " +
        "and assignee are kept. Call plan_work_item_space_move first to see the mapping. Returns the moved item " +
        "with its new version and changed fields.")]
    public Task<WorkItemWriteResponse> MoveWorkItemToSpace(
        [Description("The work item's id.")] Guid id,
        [Description("The Space to move it to. Use list_spaces to find valid ids.")] Guid targetSpaceId,
        [Description("Id of a work item in the target Space to move it under. Omit to make it a top-level item there.")] Guid? parentId = null,
        [Description("Status id in the item's Space → status id in the target Space (from list_statuses). Statuses left out take the default.")]
        IReadOnlyDictionary<Guid, Guid>? statusMap = null,
        [Description("Type id in the item's Space → Type id in the target Space (from list_work_item_types), or null to clear it. Types left out take the default.")]
        IReadOnlyDictionary<Guid, Guid?>? typeMap = null,
        [Description("Priority id in the item's Space → priority id in the target Space (from list_work_item_priorities), or null to clear it. Priorities left out take the default.")]
        IReadOnlyDictionary<Guid, Guid?>? priorityMap = null,
        [Description(ExpectedVersionArgument.Description)] uint? expectedVersion = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            _caller.UseActorLabel(actorLabel);
            var maps = SpaceMoveMaps.From(statusMap, typeMap, priorityMap);
            var result = await _moves.MoveAsync(id, targetSpaceId, parentId, maps, expectedVersion, ct);
            return await _dtos.ToWriteResponseAsync(result);
        });
}
