using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record BoardDto(Guid Id, string Name, Guid? ScopeItemId)
{
    public static BoardDto FromEntity(Board board) => new(board.Id, board.Name, board.ScopeItemId);
}

public record CreateBoardRequest(string Name, Guid? ScopeItemId);
