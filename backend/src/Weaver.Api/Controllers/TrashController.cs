using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Deleting a work item moves it to the Trash, which humans and read-write agents can list and
/// restore from (ADR-0007). Only humans can purge it; agents get 403 whatever their key's scope.
/// Read-only keys reach only the GET, through the global write filter.
/// </summary>
[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class TrashController(IWorkItemTrashService trash, IWorkItemPurgeService purge, WorkItemDtoMapper dtos) : ControllerBase
{
    private readonly IWorkItemTrashService _trash = trash;
    private readonly IWorkItemPurgeService _purge = purge;
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

    /// <summary>
    /// Purges a delete for good: the item it was made on and everything deleted with it, with
    /// their links, comments and attachments. An item deleted along with an ancestor is purged
    /// by purging that ancestor.
    /// </summary>
    [HttpDelete("api/trash/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Purge(Guid id)
    {
        await _purge.PurgeAsync(id);
        return NoContent();
    }

    /// <summary>Purges every delete in the Trash, in every Space.</summary>
    [HttpDelete("api/trash")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EmptyTrash()
    {
        await _purge.EmptyTrashAsync();
        return NoContent();
    }
}
