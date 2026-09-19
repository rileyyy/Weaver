using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/work-items")]
public class WorkItemsController : ControllerBase
{
    private readonly IWorkItemService _workItems;

    public WorkItemsController(IWorkItemService workItems)
    {
        _workItems = workItems;
    }

    /// <summary>
    /// Direct children of <paramref name="parentId"/> (or top-level items when omitted).
    /// The board uses this twice: once for swimlanes (children of the board's scope item),
    /// once per swimlane for its cards (children of that swimlane item).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkItemDto>>> GetChildren([FromQuery] Guid? parentId)
    {
        var items = await _workItems.GetChildrenAsync(parentId);
        return Ok(items.Select(WorkItemDto.FromEntity));
    }

    /// <summary>
    /// Every work item, flat and unscoped. Used by the Hierarchy view to
    /// build a full parent/child tree client-side — see
    /// <see cref="IWorkItemService.GetAllAsync"/>.
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<IReadOnlyList<WorkItemDto>>> GetAll()
    {
        var items = await _workItems.GetAllAsync();
        return Ok(items.Select(WorkItemDto.FromEntity));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkItemDto>> GetById(Guid id)
    {
        var item = await _workItems.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(WorkItemDto.FromEntity(item));
    }

    [HttpPost]
    public async Task<ActionResult<WorkItemDto>> Create(CreateWorkItemRequest request)
    {
        var item = await _workItems.CreateAsync(
            request.Title,
            request.Description,
            request.ParentId,
            request.StatusId,
            request.AfterId,
            request.LayerId,
            request.Priority);

        var dto = WorkItemDto.FromEntity(item);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>
    /// Moves a work item to a different column. Cannot reparent — see <see cref="Reparent"/>.
    /// </summary>
    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<WorkItemDto>> ChangeStatus(Guid id, ChangeWorkItemStatusRequest request)
    {
        var item = await _workItems.ChangeStatusAsync(id, request.StatusId, request.AfterId);
        return Ok(WorkItemDto.FromEntity(item));
    }

    /// <summary>
    /// Moves a work item to a different parent. Cannot change its status — see <see cref="ChangeStatus"/>.
    /// </summary>
    [HttpPost("{id:guid}/parent")]
    public async Task<ActionResult<WorkItemDto>> Reparent(Guid id, ReparentWorkItemRequest request)
    {
        var item = await _workItems.ReparentAsync(id, request.ParentId, request.AfterId);
        return Ok(WorkItemDto.FromEntity(item));
    }

    /// <summary>
    /// Sets a work item's scheduled start/end. Independent of status and
    /// parent — see <see cref="ChangeStatus"/>/<see cref="Reparent"/>.
    /// </summary>
    [HttpPost("{id:guid}/schedule")]
    public async Task<ActionResult<WorkItemDto>> Reschedule(Guid id, RescheduleWorkItemRequest request)
    {
        var item = await _workItems.RescheduleAsync(id, request.StartDate, request.EndDate);
        return Ok(WorkItemDto.FromEntity(item));
    }

    /// <summary>
    /// Updates title/description/layer/priority. Independent of status,
    /// parent, schedule, and assignee — see their own endpoints.
    /// </summary>
    [HttpPut("{id:guid}/details")]
    public async Task<ActionResult<WorkItemDto>> UpdateDetails(Guid id, UpdateWorkItemDetailsRequest request)
    {
        var item = await _workItems.UpdateDetailsAsync(
            id,
            request.Title,
            request.Description,
            request.LayerId,
            request.Priority);
        return Ok(WorkItemDto.FromEntity(item));
    }

    /// <summary>
    /// Sets or clears who a work item is assigned to. Independent of every
    /// other field.
    /// </summary>
    [HttpPost("{id:guid}/assignee")]
    public async Task<ActionResult<WorkItemDto>> Assign(Guid id, AssignWorkItemRequest request)
    {
        var item = await _workItems.AssignAsync(id, request.UserId);
        return Ok(WorkItemDto.FromEntity(item));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool cascade = false)
    {
        await _workItems.DeleteAsync(id, cascade);
        return NoContent();
    }
}
