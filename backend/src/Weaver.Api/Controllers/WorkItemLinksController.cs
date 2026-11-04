using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Controllers;

[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class WorkItemLinksController(IWorkItemLinkService links) : ControllerBase
{
    private readonly IWorkItemLinkService _links = links;

    [HttpGet("api/work-items/{workItemId:guid}/links")]
    public async Task<ActionResult<IReadOnlyList<WorkItemLinkDto>>> ListForWorkItem(Guid workItemId)
    {
        var links = await _links.ListForWorkItemAsync(workItemId);
        return Ok(links.Select(WorkItemLinkDto.FromView));
    }

    /// <summary>
    /// A link has no view of its own (it always reads from one side), so the Location header
    /// points at the list it now appears in. The type is how <paramref name="workItemId"/>
    /// relates to the target; a blocked-by or duplicated-by link is stored the other way round.
    /// </summary>
    [HttpPost("api/work-items/{workItemId:guid}/links")]
    [ProducesResponseType<WorkItemLinkDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkItemLinkDto>> Create(Guid workItemId, CreateWorkItemLinkRequest request)
    {
        var type = request.Type
            ?? throw new DomainValidationException("Link type is required: Related, Blocks, BlockedBy, Duplicates or DuplicatedBy.");
        var link = await _links.CreateAsync(workItemId, request.TargetWorkItemId, type);
        return CreatedAtAction(nameof(ListForWorkItem), new { workItemId }, WorkItemLinkDto.FromView(link));
    }

    [HttpDelete("api/work-item-links/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _links.DeleteAsync(id);
        return NoContent();
    }
}
