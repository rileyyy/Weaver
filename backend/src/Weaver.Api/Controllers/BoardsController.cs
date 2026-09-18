using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Contracts;
using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/boards")]
public class BoardsController : ControllerBase
{
    private readonly WeaverDbContext _db;

    public BoardsController(WeaverDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BoardDto>>> GetAll()
    {
        var boards = await _db.Boards.ToListAsync();
        return Ok(boards.Select(BoardDto.FromEntity));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BoardDto>> GetById(Guid id)
    {
        var board = await _db.Boards.FindAsync(id);
        return board is null ? NotFound() : Ok(BoardDto.FromEntity(board));
    }

    [HttpPost]
    public async Task<ActionResult<BoardDto>> Create(CreateBoardRequest request)
    {
        if (request.ScopeItemId is not null &&
            !await _db.WorkItems.AnyAsync(w => w.Id == request.ScopeItemId))
        {
            return NotFound($"Work item {request.ScopeItemId} was not found.");
        }

        var board = new Board
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ScopeItemId = request.ScopeItemId,
        };

        _db.Boards.Add(board);
        await _db.SaveChangesAsync();

        var dto = BoardDto.FromEntity(board);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }
}
