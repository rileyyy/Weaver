using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/boards")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public class BoardsController : ControllerBase
{
    private readonly IBoardService _boards;

    public BoardsController(IBoardService boards)
    {
        _boards = boards;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BoardDto>>> GetAll()
    {
        var boards = await _boards.GetAllAsync();
        return Ok(boards.Select(BoardDto.FromEntity));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BoardDto>> GetById(Guid id)
    {
        var board = await _boards.GetByIdAsync(id);
        return board is null ? NotFound() : Ok(BoardDto.FromEntity(board));
    }

    [HttpPost]
    [ProducesResponseType<BoardDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BoardDto>> Create(CreateBoardRequest request)
    {
        var board = await _boards.CreateAsync(request.Name, request.ScopeItemId);

        var dto = BoardDto.FromEntity(board);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }
}
