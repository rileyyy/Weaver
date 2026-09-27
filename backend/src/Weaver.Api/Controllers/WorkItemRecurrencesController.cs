using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public class WorkItemRecurrencesController : ControllerBase
{
    private readonly IWorkItemRecurrenceService _recurrences;

    public WorkItemRecurrencesController(IWorkItemRecurrenceService recurrences)
    {
        _recurrences = recurrences;
    }

    [HttpGet("api/recurrences")]
    public async Task<ActionResult<IReadOnlyList<WorkItemRecurrenceDto>>> List()
    {
        var recurrences = await _recurrences.ListAsync();
        return Ok(recurrences.Select(WorkItemRecurrenceDto.FromModel));
    }

    /// <summary>204 when the work item exists but doesn't repeat; 404 when it doesn't exist.</summary>
    [HttpGet("api/work-items/{workItemId:guid}/recurrence")]
    public async Task<ActionResult<WorkItemRecurrenceDto>> Get(Guid workItemId)
    {
        var recurrence = await _recurrences.GetAsync(workItemId);
        return recurrence is null ? NoContent() : Ok(WorkItemRecurrenceDto.FromModel(recurrence));
    }

    [HttpPut("api/work-items/{workItemId:guid}/recurrence")]
    public async Task<ActionResult<WorkItemRecurrenceDto>> Set(Guid workItemId, SetWorkItemRecurrenceRequest request)
    {
        var recurrence = await _recurrences.SetAsync(
            workItemId,
            request.Frequency,
            request.DaysOfWeek ?? [],
            request.StartDate,
            request.EndDate);
        return Ok(WorkItemRecurrenceDto.FromModel(recurrence));
    }

    [HttpDelete("api/work-items/{workItemId:guid}/recurrence")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid workItemId)
    {
        await _recurrences.RemoveAsync(workItemId);
        return NoContent();
    }
}
