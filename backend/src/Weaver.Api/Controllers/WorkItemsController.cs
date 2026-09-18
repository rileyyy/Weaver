using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Contracts;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/work-items")]
public class WorkItemsController : ControllerBase
{
    private readonly WeaverDbContext _db;
    private readonly IWorkItemService _workItems;

    public WorkItemsController(WeaverDbContext db, IWorkItemService workItems)
    {
        _db = db;
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
        var items = await _db.WorkItems
            .Where(w => w.ParentId == parentId)
            .OrderBy(w => w.Rank)
            .ToListAsync();

        return Ok(items.Select(WorkItemDto.FromEntity));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkItemDto>> GetById(Guid id)
    {
        var item = await _db.WorkItems.FindAsync(id);
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
            request.AfterId);

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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool cascade = false)
    {
        await _workItems.DeleteAsync(id, cascade);
        return NoContent();
    }
}
