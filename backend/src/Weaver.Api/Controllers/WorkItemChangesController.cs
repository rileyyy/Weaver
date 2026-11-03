using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
public class WorkItemChangesController : ControllerBase
{
    private readonly IWorkItemChangesService _changes;

    public WorkItemChangesController(IWorkItemChangesService changes)
    {
        _changes = changes;
    }

    /// <summary>
    /// The work items changed at or after <paramref name="since"/>, each with its history entries
    /// since then, deletes included. Pages in the order each item first changed; send the
    /// returned <c>nextCursor</c>, with the same query, for the next page.
    /// </summary>
    [HttpGet("api/work-items/changes")]
    public async Task<ActionResult<WorkItemChangesPageDto>> ListSince(
        [FromQuery, BindRequired] DateTimeOffset since,
        [FromQuery] Guid? spaceId = null,
        [FromQuery] bool excludeOwnChanges = false,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = WorkItemChangesQuery.DefaultLimit)
    {
        var page = await _changes.ListSinceAsync(new WorkItemChangesQuery(since, spaceId, excludeOwnChanges, cursor, limit));
        return Ok(WorkItemChangesPageDto.FromPage(page));
    }
}
