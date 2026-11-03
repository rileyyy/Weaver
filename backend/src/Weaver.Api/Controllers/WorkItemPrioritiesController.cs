using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Each Space has its own priorities (ADR-0001). Anyone signed in can list a Space's; changing
/// them needs an admin (403 otherwise).
/// </summary>
[ApiController]
[Route("api/work-item-priorities")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class WorkItemPrioritiesController(IWorkItemPriorityService priorities) : ControllerBase
{
    private readonly IWorkItemPriorityService _priorities = priorities;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkItemPriorityDto>>> GetAll([FromQuery, BindRequired] Guid spaceId)
    {
        var priorities = await _priorities.GetAllAsync(spaceId);
        return Ok(priorities.Select(WorkItemPriorityDto.FromEntity));
    }

    /// <summary>A priority has no view of its own, so the Location header points at the list.</summary>
    [HttpPost]
    [ProducesResponseType<WorkItemPriorityDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkItemPriorityDto>> Create(CreatePriorityRequest request)
    {
        var created = await _priorities.CreateAsync(User.GetUserId(), request.SpaceId, request.Name);
        return CreatedAtAction(nameof(GetAll), new { spaceId = created.SpaceId }, WorkItemPriorityDto.FromEntity(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkItemPriorityDto>> Rename(Guid id, LookupNameRequest request)
    {
        var renamed = await _priorities.RenameAsync(User.GetUserId(), id, request.Name);
        return Ok(WorkItemPriorityDto.FromEntity(renamed));
    }

    /// <summary><c>orderedIds</c> must list every priority of <paramref name="spaceId"/> exactly once.</summary>
    [HttpPut("order")]
    public async Task<ActionResult<IReadOnlyList<WorkItemPriorityDto>>> Reorder(
        [FromQuery, BindRequired] Guid spaceId,
        ReorderLookupRequest request)
    {
        var priorities = await _priorities.ReorderAsync(User.GetUserId(), spaceId, request.OrderedIds);
        return Ok(priorities.Select(WorkItemPriorityDto.FromEntity));
    }

    /// <summary>
    /// Items using the deleted priority move to <paramref name="replacementId"/>, a priority of
    /// the same Space, or to none when it's omitted.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? replacementId)
    {
        await _priorities.DeleteAsync(User.GetUserId(), id, replacementId);
        return NoContent();
    }
}
