using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class BoardTools
{
    private readonly IBoardService _boards;

    public BoardTools(IBoardService boards)
    {
        _boards = boards;
    }

    [McpServerTool(Name = "list_boards", ReadOnly = true)]
    [Description("Lists every board.")]
    public async Task<IReadOnlyList<BoardDto>> ListBoards(CancellationToken ct)
    {
        var boards = await _boards.GetAllAsync(ct);
        return boards.Select(BoardDto.FromEntity).ToList();
    }

    [McpServerTool(Name = "get_board", ReadOnly = true)]
    [Description("Gets a single board by id.")]
    public async Task<BoardDto> GetBoard(
        [Description("The board's id.")] Guid id,
        CancellationToken ct)
    {
        var board = await _boards.GetByIdAsync(id, ct);
        return board is not null ? BoardDto.FromEntity(board) : throw new McpException($"Board {id} was not found.");
    }

    [McpServerTool(Name = "create_board", Destructive = false)]
    [Description("Creates a new board, optionally scoped to a work item (its swimlanes are then " +
        "that item's direct children).")]
    public Task<BoardDto> CreateBoard(
        [Description("Name of the board.")] string name,
        [Description("Id of the work item this board is scoped to. Omit for a board of top-level work items.")] Guid? scopeItemId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var board = await _boards.CreateAsync(name, scopeItemId, ct);
            return BoardDto.FromEntity(board);
        });
}
