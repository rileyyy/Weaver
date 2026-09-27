namespace Weaver.Api.Contracts;

public record CreateBoardRequest(string Name, Guid? ScopeItemId);
