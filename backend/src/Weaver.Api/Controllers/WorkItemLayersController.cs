using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/work-item-layers")]
public class WorkItemLayersController : ControllerBase
{
    private readonly IWorkItemLayerService _layers;

    public WorkItemLayersController(IWorkItemLayerService layers)
    {
        _layers = layers;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkItemLayerDto>>> GetAll()
    {
        var layers = await _layers.GetAllAsync();
        return Ok(layers.Select(WorkItemLayerDto.FromEntity));
    }
}
