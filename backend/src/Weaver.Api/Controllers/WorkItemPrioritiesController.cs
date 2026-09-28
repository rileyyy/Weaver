using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>Anyone signed in can list prioritys; changing them needs an admin (403 otherwise).</summary>
[ApiController]
[Route("api/work-item-priorities")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class WorkItemPrioritiesController : ControllerBase
{
    private readonly IWorkItemPriorityService _priorities;

    public WorkItemPrioritiesController(IWorkItemPriorityService priorities)
    {
        _priorities = priorities;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkItemPriorityDto>>> GetAll()
    {
        var priorities = await _priorities.GetAllAsync();
        return Ok(priorities.Select(WorkItemPriorityDto.FromEntity));
    }

    /// <summary>A priority has no view of its own, so the Location header points at the list.</summary>
    [HttpPost]
    [ProducesResponseType<WorkItemPriorityDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkItemPriorityDto>> Create(LookupNameRequest request)
    {
        var created = await _priorities.CreateAsync(User.GetUserId(), request.Name);
        return CreatedAtAction(nameof(GetAll), WorkItemPriorityDto.FromEntity(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkItemPriorityDto>> Rename(Guid id, LookupNameRequest request)
    {
        var renamed = await _priorities.RenameAsync(User.GetUserId(), id, request.Name);
        return Ok(WorkItemPriorityDto.FromEntity(renamed));
    }

    [HttpPut("order")]
    public async Task<ActionResult<IReadOnlyList<WorkItemPriorityDto>>> Reorder(ReorderLookupRequest request)
    {
        var priorities = await _priorities.ReorderAsync(User.GetUserId(), request.OrderedIds);
        return Ok(priorities.Select(WorkItemPriorityDto.FromEntity));
    }

    /// <summary>Items using the deleted priority move to <paramref name="replacementId"/>, or to none when it's omitted.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? replacementId)
    {
        await _priorities.DeleteAsync(User.GetUserId(), id, replacementId);
        return NoContent();
    }
}
