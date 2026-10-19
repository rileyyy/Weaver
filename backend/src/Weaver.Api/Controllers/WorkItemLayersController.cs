using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>Anyone signed in can list layers; changing them needs an admin (403 otherwise).</summary>
[ApiController]
[Route("api/work-item-layers")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
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

    /// <summary>A layer has no view of its own, so the Location header points at the list.</summary>
    [HttpPost]
    [ProducesResponseType<WorkItemLayerDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkItemLayerDto>> Create(LayerDetailsRequest request)
    {
        var created = await _layers.CreateAsync(User.GetUserId(), request.Name, request.Color);
        return CreatedAtAction(nameof(GetAll), WorkItemLayerDto.FromEntity(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkItemLayerDto>> Update(Guid id, LayerDetailsRequest request)
    {
        var updated = await _layers.UpdateAsync(User.GetUserId(), id, request.Name, request.Color);
        return Ok(WorkItemLayerDto.FromEntity(updated));
    }

    [HttpPut("order")]
    public async Task<ActionResult<IReadOnlyList<WorkItemLayerDto>>> Reorder(ReorderLookupRequest request)
    {
        var layers = await _layers.ReorderAsync(User.GetUserId(), request.OrderedIds);
        return Ok(layers.Select(WorkItemLayerDto.FromEntity));
    }

    /// <summary>Items using the deleted layer move to <paramref name="replacementId"/>, or to none when it's omitted.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? replacementId)
    {
        await _layers.DeleteAsync(User.GetUserId(), id, replacementId);
        return NoContent();
    }
}
