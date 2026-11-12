using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Read-only on purpose, like work item history: <see cref="ISpaceService"/> appends the entries
/// and nothing edits or deletes one. Anyone signed in can read it, as anyone can list Spaces.
/// </summary>
[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public class SpaceHistoryController(ISpaceHistoryService history) : ControllerBase
{
    private readonly ISpaceHistoryService _history = history;

    /// <summary>Every change to the Space (creation, renames, settings), newest first.</summary>
    [HttpGet("api/spaces/{spaceId:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<SpaceHistoryEntryDto>>> ListForSpace(Guid spaceId)
    {
        var entries = await _history.ListForSpaceAsync(spaceId);
        return Ok(entries.Select(SpaceHistoryEntryDto.FromView));
    }
}
