using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Each Space has its own Types (ADR-0002). Anyone signed in can list a Space's; changing them
/// needs an admin (403 otherwise).
/// </summary>
[ApiController]
[Route("api/work-item-types")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class WorkItemTypesController(IWorkItemTypeService types) : ControllerBase
{
    private readonly IWorkItemTypeService _types = types;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkItemTypeDto>>> GetAll([FromQuery, BindRequired] Guid spaceId)
    {
        var types = await _types.GetAllAsync(spaceId);
        return Ok(types.Select(WorkItemTypeDto.FromEntity));
    }

    /// <summary>A Type has no view of its own, so the Location header points at the list.</summary>
    [HttpPost]
    [ProducesResponseType<WorkItemTypeDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkItemTypeDto>> Create(CreateTypeRequest request)
    {
        var created = await _types.CreateAsync(User.GetUserId(), request.SpaceId, request.Name, request.Color);
        return CreatedAtAction(nameof(GetAll), new { spaceId = created.SpaceId }, WorkItemTypeDto.FromEntity(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkItemTypeDto>> Update(Guid id, TypeDetailsRequest request)
    {
        var updated = await _types.UpdateAsync(User.GetUserId(), id, request.Name, request.Color);
        return Ok(WorkItemTypeDto.FromEntity(updated));
    }

    /// <summary><c>orderedIds</c> must list every Type of <paramref name="spaceId"/> exactly once.</summary>
    [HttpPut("order")]
    public async Task<ActionResult<IReadOnlyList<WorkItemTypeDto>>> Reorder(
        [FromQuery, BindRequired] Guid spaceId,
        ReorderLookupRequest request)
    {
        var types = await _types.ReorderAsync(User.GetUserId(), spaceId, request.OrderedIds);
        return Ok(types.Select(WorkItemTypeDto.FromEntity));
    }

    /// <summary>
    /// Items using the deleted Type move to <paramref name="replacementId"/>, a Type of the same
    /// Space, or to none when it's omitted.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? replacementId)
    {
        await _types.DeleteAsync(User.GetUserId(), id, replacementId);
        return NoContent();
    }
}
