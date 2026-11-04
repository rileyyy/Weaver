using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Deleting a work item moves it to the Trash, which humans and read-write agents can list and
/// restore from (ADR-0007). Read-only keys reach only the GET, through the global write filter.
/// </summary>
[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class TrashController(IWorkItemTrashService trash, WorkItemDtoMapper dtos) : ControllerBase
{
    private readonly IWorkItemTrashService _trash = trash;
    private readonly WorkItemDtoMapper _dtos = dtos;

    /// <summary>
    /// Moves the work item and its subtree to the Trash. An item with children needs
    /// <paramref name="cascade"/>=true.
    /// </summary>
    [HttpDelete("api/work-items/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool cascade = false, [FromQuery] uint? expectedVersion = null)
    {
        await _trash.MoveToTrashAsync(id, cascade, expectedVersion);
        return NoContent();
    }

    /// <summary>Each delete in the Trash, newest first; only <paramref name="spaceId"/>'s when given.</summary>
    [HttpGet("api/trash")]
    public async Task<ActionResult<IReadOnlyList<TrashedWorkItemDto>>> List([FromQuery] Guid? spaceId)
    {
        var trash = await _trash.ListAsync(spaceId);
        return Ok(trash.Select(TrashedWorkItemDto.FromModel));
    }

    /// <summary>
    /// Restores a deleted item and everything deleted with it, under its nearest ancestor that
    /// still exists, or at the top of its Space.
    /// </summary>
    [HttpPost("api/trash/{id:guid}/restore")]
    public async Task<ActionResult<WorkItemDto>> Restore(Guid id)
    {
        var item = await _trash.RestoreAsync(id);
        return Ok(await _dtos.ToDtoAsync(item));
    }
}
