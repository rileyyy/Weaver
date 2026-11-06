using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Controllers;

/// <summary>
/// The custom fields each Type defines (ADR-0002). Anyone signed in can list a Type's; changing
/// them needs a human admin (403 otherwise).
/// </summary>
[ApiController]
[Route("api/custom-fields")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class CustomFieldsController(ICustomFieldService fields) : ControllerBase
{
    private readonly ICustomFieldService _fields = fields;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDto>>> GetAll([FromQuery, BindRequired] Guid typeId)
    {
        var fields = await _fields.GetAllAsync(typeId);
        return Ok(fields.Select(CustomFieldDto.FromEntity));
    }

    /// <summary>Every Type's fields in the Space, in each Type's order, for showing the Space's whole tree.</summary>
    [HttpGet("/api/spaces/{spaceId:guid}/custom-fields")]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDto>>> GetAllInSpace(Guid spaceId)
    {
        var fields = await _fields.GetAllInSpaceAsync(spaceId);
        return Ok(fields.Select(CustomFieldDto.FromEntity));
    }

    /// <summary>A field has no view of its own, so the Location header points at the list.</summary>
    [HttpPost]
    [ProducesResponseType<CustomFieldDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomFieldDto>> Create(CreateCustomFieldRequest request)
    {
        var created = await _fields.CreateAsync(
            User.GetUserId(), request.TypeId, request.Name, request.Kind, request.Options ?? [], request.RollsUp);
        return CreatedAtAction(nameof(GetAll), new { typeId = created.TypeId }, CustomFieldDto.FromEntity(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomFieldDto>> Update(Guid id, UpdateCustomFieldRequest request)
    {
        var options = (request.Options ?? []).Select(o => new CustomFieldOptionRevision(o.Id, o.Name)).ToList();
        var updated = await _fields.UpdateAsync(User.GetUserId(), id, request.Name, options, request.RollsUp);
        return Ok(CustomFieldDto.FromEntity(updated));
    }

    /// <summary><c>orderedIds</c> must list every field of <paramref name="typeId"/> exactly once.</summary>
    [HttpPut("order")]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDto>>> Reorder(
        [FromQuery, BindRequired] Guid typeId,
        ReorderLookupRequest request)
    {
        var fields = await _fields.ReorderAsync(User.GetUserId(), typeId, request.OrderedIds);
        return Ok(fields.Select(CustomFieldDto.FromEntity));
    }

    /// <summary>Removes the field and every item's value for it; each item's history keeps the value.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _fields.DeleteAsync(User.GetUserId(), id);
        return NoContent();
    }
}
