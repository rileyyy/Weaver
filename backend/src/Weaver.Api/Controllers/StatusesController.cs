using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Each Space has its own statuses (ADR-0001). Anyone signed in can list a Space's; changing
/// them needs an admin (403 otherwise).
/// </summary>
[ApiController]
[Route("api/statuses")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class StatusesController(IStatusService statuses) : ControllerBase
{
    private readonly IStatusService _statuses = statuses;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StatusDto>>> GetAll([FromQuery, BindRequired] Guid spaceId)
    {
        var statuses = await _statuses.GetAllAsync(spaceId);
        return Ok(statuses.Select(StatusDto.FromEntity));
    }

    /// <summary>A status has no view of its own, so the Location header points at the list.</summary>
    [HttpPost]
    [ProducesResponseType<StatusDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<StatusDto>> Create(CreateStatusRequest request)
    {
        var status = await _statuses.CreateAsync(
            User.GetUserId(), request.SpaceId, request.Name, request.Category, request.Color);
        return CreatedAtAction(nameof(GetAll), new { spaceId = status.SpaceId }, StatusDto.FromEntity(status));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StatusDto>> Update(Guid id, StatusDetailsRequest request)
    {
        var status = await _statuses.UpdateAsync(User.GetUserId(), id, request.Name, request.Category, request.Color);
        return Ok(StatusDto.FromEntity(status));
    }

    /// <summary><c>orderedIds</c> must list every status of <paramref name="spaceId"/> exactly once.</summary>
    [HttpPut("order")]
    public async Task<ActionResult<IReadOnlyList<StatusDto>>> Reorder(
        [FromQuery, BindRequired] Guid spaceId,
        ReorderLookupRequest request)
    {
        var statuses = await _statuses.ReorderAsync(User.GetUserId(), spaceId, request.OrderedIds);
        return Ok(statuses.Select(StatusDto.FromEntity));
    }

    /// <summary>
    /// Items in the deleted status move to <paramref name="replacementId"/>, a status of the same
    /// Space, which is required while any exist.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? replacementId)
    {
        await _statuses.DeleteAsync(User.GetUserId(), id, replacementId);
        return NoContent();
    }
}
