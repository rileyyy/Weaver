using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
public class WorkItemSearchController(IWorkItemQueryService query) : ControllerBase
{
    private readonly IWorkItemQueryService _query = query;

    /// <summary>
    /// The work-item query every client shares (ADR-0006): the inputs given AND together, and
    /// each list input matches any of its values. Pages in Number order; send the returned
    /// <c>nextCursor</c>, with the same query, for the next page.
    /// </summary>
    [HttpGet("api/work-items/search")]
    public async Task<ActionResult<WorkItemPageDto>> Search([FromQuery] WorkItemSearchParameters parameters)
    {
        var page = await _query.QueryAsync(parameters.ToQuery());
        return Ok(WorkItemPageDto.FromPage(page));
    }
}
