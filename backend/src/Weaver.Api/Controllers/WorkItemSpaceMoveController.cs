using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Moving a work item and its subtree into another Space (ADR-0001), which reparenting refuses
/// to do: read the plan to show the mapping, then move with any values remapped. A read-only key
/// reaches only the plan, through the global write filter.
/// </summary>
[ApiController]
[Route("api/work-items/{id:guid}")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class WorkItemSpaceMoveController(IWorkItemSpaceMoveService moves, WorkItemDtoMapper dtos) : ControllerBase
{
    private readonly IWorkItemSpaceMoveService _moves = moves;
    private readonly WorkItemDtoMapper _dtos = dtos;

    /// <summary>
    /// How many items a move to <paramref name="targetSpaceId"/> takes, how many custom field
    /// values it clears, and where each status, Type and priority they use goes by default.
    /// Changes nothing.
    /// </summary>
    [HttpGet("space-move-plan")]
    public async Task<ActionResult<SpaceMovePlanDto>> Plan(Guid id, [FromQuery] Guid targetSpaceId)
    {
        var plan = await _moves.PlanAsync(id, targetSpaceId);
        return Ok(SpaceMovePlanDto.FromModel(plan));
    }

    /// <summary>
    /// Moves the item and its subtree into another Space in one save, mapping each value as the
    /// request says or to its default. Returns the moved item and its changed fields.
    /// </summary>
    [HttpPost("space-move")]
    public async Task<ActionResult<WorkItemWriteResponse>> Move(Guid id, MoveWorkItemToSpaceRequest request)
    {
        var maps = SpaceMoveMaps.From(request.StatusMap, request.TypeMap, request.PriorityMap);
        var result = await _moves.MoveAsync(id, request.TargetSpaceId, request.ParentId, maps, request.ExpectedVersion);
        return Ok(await _dtos.ToWriteResponseAsync(result));
    }
}
