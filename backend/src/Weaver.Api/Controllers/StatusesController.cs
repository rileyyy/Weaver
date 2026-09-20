using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/statuses")]
public class StatusesController : ControllerBase
{
    private readonly IStatusService _statuses;

    public StatusesController(IStatusService statuses)
    {
        _statuses = statuses;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StatusDto>>> GetAll()
    {
        var statuses = await _statuses.GetAllAsync();
        return Ok(statuses.Select(StatusDto.FromEntity));
    }
}
