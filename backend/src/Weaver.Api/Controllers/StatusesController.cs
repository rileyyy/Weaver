using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Contracts;
using Weaver.Infrastructure;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/statuses")]
public class StatusesController : ControllerBase
{
    private readonly WeaverDbContext _db;

    public StatusesController(WeaverDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StatusDto>>> GetAll()
    {
        var statuses = await _db.Statuses.OrderBy(s => s.Order).ToListAsync();
        return Ok(statuses.Select(StatusDto.FromEntity));
    }
}
