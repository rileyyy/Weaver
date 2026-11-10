using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>An item's roll-ups (ADR-0002): its descendants' number and currency fields, summed when read.</summary>
[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public class WorkItemRollUpsController(IWorkItemRollUpService rollUps) : ControllerBase
{
    private readonly IWorkItemRollUpService _rollUps = rollUps;

    /// <summary>One total per rolled-up field a live descendant has a value for, the item's own Type's fields first.</summary>
    [HttpGet("api/work-items/{workItemId:guid}/roll-ups")]
    public async Task<ActionResult<IReadOnlyList<RollUpDto>>> Get(Guid workItemId)
    {
        var totals = await _rollUps.GetAsync(workItemId);
        return Ok(totals.Select(RollUpDto.FromTotal));
    }
}
