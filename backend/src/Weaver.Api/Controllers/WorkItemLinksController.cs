using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Controllers;

[ApiController]
public class WorkItemLinksController : ControllerBase
{
    private readonly IWorkItemLinkService _links;

    public WorkItemLinksController(IWorkItemLinkService links)
    {
        _links = links;
    }

    [HttpGet("api/work-items/{workItemId:guid}/links")]
    public async Task<ActionResult<IReadOnlyList<WorkItemLinkDto>>> ListForWorkItem(Guid workItemId)
    {
        var links = await _links.ListForWorkItemAsync(workItemId);
        return Ok(links.Select(l => WorkItemLinkDto.FromEntity(l, workItemId)));
    }

    [HttpPost("api/work-items/{workItemId:guid}/links")]
    public async Task<ActionResult<WorkItemLinkDto>> Create(Guid workItemId, CreateWorkItemLinkRequest request)
    {
        var link = await _links.CreateAsync(workItemId, request.TargetWorkItemId);
        return Ok(WorkItemLinkDto.FromEntity(link, workItemId));
    }

    [HttpDelete("api/work-item-links/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _links.DeleteAsync(id);
        return NoContent();
    }
}
