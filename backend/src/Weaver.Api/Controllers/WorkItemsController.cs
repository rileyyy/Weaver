using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/work-items")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class WorkItemsController(IWorkItemService workItems) : ControllerBase
{
    private readonly IWorkItemService _workItems = workItems;

    /// <summary>
    /// Direct children of <paramref name="parentId"/>, or the top-level items of
    /// <paramref name="spaceId"/> when the parent is omitted. One of the two is required.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkItemDto>>> GetChildren(
          [FromQuery] Guid? parentId,
          [FromQuery] Guid? spaceId)
    {
        var items = await _workItems.GetChildrenAsync(parentId, spaceId);
        return Ok(items.Select(WorkItemDto.FromEntity));
    }

    /// <summary>
    /// A board's lanes (children of <paramref name="scopeItemId"/>, or the top-level items of
    /// <paramref name="spaceId"/> when omitted) with each lane's cards, in one request.
    /// </summary>
    [HttpGet("swimlanes")]
    public async Task<ActionResult<IReadOnlyList<SwimlaneDto>>> GetSwimlanes(
        [FromQuery] Guid? scopeItemId,
        [FromQuery] Guid? spaceId)
    {
        var swimlanes = await _workItems.GetSwimlanesAsync(scopeItemId, spaceId);
        return Ok(swimlanes.Select(SwimlaneDto.FromSwimlane));
    }

    /// <summary>
    /// One page of every work item in <paramref name="spaceId"/> (or every Space when
    /// omitted), flat, for the Hierarchy and Roadmap views, which page until a response comes
    /// back shorter than <paramref name="limit"/> — see <see cref="IWorkItemService.GetAllAsync"/>.
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<IReadOnlyList<WorkItemDto>>> GetAll(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = WorkItemService.DefaultPageSize,
        [FromQuery] Guid? spaceId = null)
    {
        var items = await _workItems.GetAllAsync(offset, limit, spaceId);
        return Ok(items.Select(WorkItemDto.FromEntity));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkItemDto>> GetById(Guid id)
    {
        var item = await _workItems.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(WorkItemDto.FromEntity(item));
    }

    [HttpPost]
    [ProducesResponseType<WorkItemDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkItemDto>> Create(CreateWorkItemRequest request)
    {
        var item = await _workItems.CreateAsync(
            request.Title,
            request.Description,
            request.ParentId,
            request.StatusId,
            request.AfterId,
            request.LayerId,
            request.PriorityId,
            request.SpaceId);

        var dto = WorkItemDto.FromEntity(item);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>
    /// Moves a work item to a different column. Cannot reparent — see <see cref="Reparent"/>.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<WorkItemWriteResponse>> ChangeStatus(Guid id, ChangeWorkItemStatusRequest request)
    {
        var result = await _workItems.ChangeStatusAsync(id, request.StatusId, request.AfterId, request.ExpectedVersion);
        return Ok(WorkItemWriteResponse.FromResult(result));
    }

    /// <summary>
    /// Moves a work item to a different parent. Cannot change its status — see <see cref="ChangeStatus"/>.
    /// </summary>
    [HttpPut("{id:guid}/parent")]
    public async Task<ActionResult<WorkItemWriteResponse>> Reparent(Guid id, ReparentWorkItemRequest request)
    {
        var result = await _workItems.ReparentAsync(id, request.ParentId, request.AfterId, request.ExpectedVersion);
        return Ok(WorkItemWriteResponse.FromResult(result));
    }

    /// <summary>
    /// Patches a work item's scheduled start/end. Independent of status and
    /// parent — see <see cref="ChangeStatus"/>/<see cref="Reparent"/>.
    /// </summary>
    [HttpPatch("{id:guid}/schedule")]
    public async Task<ActionResult<WorkItemWriteResponse>> Reschedule(Guid id, RescheduleWorkItemRequest request)
    {
        var result = await _workItems.RescheduleAsync(id, request.StartDate, request.EndDate, request.ExpectedVersion);
        return Ok(WorkItemWriteResponse.FromResult(result));
    }

    /// <summary>
    /// Patches title/description/layer/priority/When. Independent of status,
    /// parent, schedule, and assignee — see their own endpoints.
    /// </summary>
    [HttpPatch("{id:guid}/details")]
    public async Task<ActionResult<WorkItemWriteResponse>> UpdateDetails(Guid id, UpdateWorkItemDetailsRequest request)
    {
        var patch = new WorkItemDetailsPatch
        {
            Title = request.Title,
            Description = request.Description,
            LayerId = request.LayerId,
            PriorityId = request.PriorityId,
            When = request.When,
        };
        var result = await _workItems.UpdateDetailsAsync(id, patch, request.ExpectedVersion);
        return Ok(WorkItemWriteResponse.FromResult(result));
    }

    /// <summary>
    /// Sets or clears who a work item is assigned to. Independent of every
    /// other field.
    /// </summary>
    [HttpPut("{id:guid}/assignee")]
    public async Task<ActionResult<WorkItemWriteResponse>> Assign(Guid id, AssignWorkItemRequest request)
    {
        var result = await _workItems.AssignAsync(id, request.UserId, request.ExpectedVersion);
        return Ok(WorkItemWriteResponse.FromResult(result));
    }

    /// <summary>
    /// Replaces a work item's full tag list, or clears it with <c>null</c>. Independent of
    /// every other field.
    /// </summary>
    [HttpPatch("{id:guid}/tags")]
    public async Task<ActionResult<WorkItemWriteResponse>> SetTags(Guid id, SetTagsWorkItemRequest request)
    {
        var tags = request.Tags.HasValue
            ? new Optional<IReadOnlyList<string>>(request.Tags.Value ?? [])
            : Optional<IReadOnlyList<string>>.Unset;
        var result = await _workItems.SetTagsAsync(id, tags, request.ExpectedVersion);
        return Ok(WorkItemWriteResponse.FromResult(result));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool cascade = false, [FromQuery] uint? expectedVersion = null)
    {
        await _workItems.DeleteAsync(id, cascade, expectedVersion);
        return NoContent();
    }
}
