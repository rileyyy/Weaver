using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public class WorkItemUrlLinksController(IWorkItemUrlLinkService urlLinks) : ControllerBase
{
    private readonly IWorkItemUrlLinkService _urlLinks = urlLinks;

    [HttpGet("api/work-items/{workItemId:guid}/url-links")]
    public async Task<ActionResult<IReadOnlyList<WorkItemUrlLinkDto>>> ListForWorkItem(Guid workItemId)
    {
        var links = await _urlLinks.ListForWorkItemAsync(workItemId);
        return Ok(links.Select(WorkItemUrlLinkDto.FromView));
    }

    /// <summary>
    /// The URL must be an absolute http or https address. Like a work item link, a URL link has
    /// no view of its own, so the Location header points at the item's list.
    /// </summary>
    [HttpPost("api/work-items/{workItemId:guid}/url-links")]
    [ProducesResponseType<WorkItemUrlLinkDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkItemUrlLinkDto>> Create(Guid workItemId, CreateWorkItemUrlLinkRequest request)
    {
        var link = await _urlLinks.CreateAsync(workItemId, request.Url, request.Label);
        return CreatedAtAction(nameof(ListForWorkItem), new { workItemId }, WorkItemUrlLinkDto.FromView(link));
    }

    [HttpDelete("api/work-item-url-links/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _urlLinks.DeleteAsync(id);
        return NoContent();
    }
}
