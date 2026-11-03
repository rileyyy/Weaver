using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>Anyone signed in can list Spaces; creating and renaming them needs a human admin (403 otherwise).</summary>
[ApiController]
[Route("api/spaces")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class SpacesController(ISpaceService spaces) : ControllerBase
{
    private readonly ISpaceService _spaces = spaces;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SpaceDto>>> GetAll()
    {
        var spaces = await _spaces.GetAllAsync();
        return Ok(spaces.Select(SpaceDto.FromEntity));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpaceDto>> GetById(Guid id)
    {
        var space = await _spaces.GetByIdAsync(id);
        return space is null ? NotFound() : Ok(SpaceDto.FromEntity(space));
    }

    [HttpPost]
    [ProducesResponseType<SpaceDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SpaceDto>> Create(SpaceNameRequest request)
    {
        var dto = SpaceDto.FromEntity(await _spaces.CreateAsync(request.Name));
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SpaceDto>> Rename(Guid id, SpaceNameRequest request)
    {
        var space = await _spaces.RenameAsync(id, request.Name);
        return Ok(SpaceDto.FromEntity(space));
    }
}
