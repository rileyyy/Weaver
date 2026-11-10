using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Saved Views are shared: anyone signed in lists them, and anyone who may write saves, changes
/// or deletes one. Overview is listed first and can't be changed or deleted (400). The default
/// View is the caller's own.
/// </summary>
[ApiController]
[Route("api/views")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class ViewsController(IViewService views) : ControllerBase
{
    private readonly IViewService _views = views;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ViewDto>>> GetAll()
    {
        var views = await _views.ListAsync();
        return Ok(views.Select(ViewDto.FromEntity));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ViewDto>> GetById(Guid id) =>
        Ok(ViewDto.FromEntity(await _views.GetAsync(id)));

    [HttpPost]
    [ProducesResponseType<ViewDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ViewDto>> Create(SaveViewRequest request)
    {
        var dto = ViewDto.FromEntity(await _views.CreateAsync(request.ToDefinition()));
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ViewDto>> Update(Guid id, SaveViewRequest request) =>
        Ok(ViewDto.FromEntity(await _views.UpdateAsync(id, request.ToDefinition())));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _views.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>The caller's default View: Overview until they pick another.</summary>
    [HttpGet("default")]
    public async Task<ActionResult<ViewDto>> GetDefault() =>
        Ok(ViewDto.FromEntity(await _views.GetDefaultAsync()));

    [HttpPut("default")]
    public async Task<ActionResult<ViewDto>> SetDefault(SetDefaultViewRequest request) =>
        Ok(ViewDto.FromEntity(await _views.SetDefaultAsync(request.ViewId)));
}
