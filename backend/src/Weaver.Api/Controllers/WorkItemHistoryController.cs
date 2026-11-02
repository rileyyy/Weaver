using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Read-only on purpose: history is append-only, written by the services that change work
/// items, so there is no endpoint that edits or deletes an entry.
/// </summary>
[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public class WorkItemHistoryController : ControllerBase
{
    private readonly IWorkItemHistoryService _history;

    public WorkItemHistoryController(IWorkItemHistoryService history)
    {
        _history = history;
    }

    /// <summary>Every change to the work item, newest first. Still answers after the item is deleted.</summary>
    [HttpGet("api/work-items/{workItemId:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<WorkItemHistoryEntryDto>>> ListForWorkItem(Guid workItemId)
    {
        var entries = await _history.ListForWorkItemAsync(workItemId);
        return Ok(entries.Select(WorkItemHistoryEntryDto.FromView));
    }
}
