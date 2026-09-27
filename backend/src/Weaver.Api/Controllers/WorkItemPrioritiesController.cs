using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/work-item-priorities")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
}
