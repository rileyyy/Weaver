using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>Anyone signed in can list types; changing them needs an admin (403 otherwise).</summary>
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
    public async Task<ActionResult<IReadOnlyList<WorkItemTypeDto>>> GetAll()
    {
        var types = await _types.GetAllAsync();
        return Ok(types.Select(WorkItemTypeDto.FromEntity));
    }

    /// <summary>A type has no view of its own, so the Location header points at the list.</summary>
    [HttpPost]
    [ProducesResponseType<WorkItemTypeDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkItemTypeDto>> Create(TypeDetailsRequest request)
    {
        var created = await _types.CreateAsync(User.GetUserId(), request.Name, request.Color);
        return CreatedAtAction(nameof(GetAll), WorkItemTypeDto.FromEntity(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkItemTypeDto>> Update(Guid id, TypeDetailsRequest request)
    {
        var updated = await _types.UpdateAsync(User.GetUserId(), id, request.Name, request.Color);
        return Ok(WorkItemTypeDto.FromEntity(updated));
    }

    [HttpPut("order")]
    public async Task<ActionResult<IReadOnlyList<WorkItemTypeDto>>> Reorder(ReorderLookupRequest request)
    {
        var types = await _types.ReorderAsync(User.GetUserId(), request.OrderedIds);
        return Ok(types.Select(WorkItemTypeDto.FromEntity));
    }

    /// <summary>Items using the deleted type move to <paramref name="replacementId"/>, or to none when it's omitted.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? replacementId)
    {
        await _types.DeleteAsync(User.GetUserId(), id, replacementId);
        return NoContent();
    }
}
