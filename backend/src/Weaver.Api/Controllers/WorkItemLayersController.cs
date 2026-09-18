using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Contracts;
using Weaver.Infrastructure;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/work-item-layers")]
public class WorkItemLayersController : ControllerBase
{
    private readonly WeaverDbContext _db;

    public WorkItemLayersController(WeaverDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkItemLayerDto>>> GetAll()
    {
        var layers = await _db.WorkItemLayers.OrderBy(l => l.Order).ToListAsync();
        return Ok(layers.Select(WorkItemLayerDto.FromEntity));
    }
}
