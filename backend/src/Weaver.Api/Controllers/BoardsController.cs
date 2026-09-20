using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/boards")]
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
    public async Task<ActionResult<BoardDto>> Create(CreateBoardRequest request)
    {
        var board = await _boards.CreateAsync(request.Name, request.ScopeItemId);

        var dto = BoardDto.FromEntity(board);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }
}
