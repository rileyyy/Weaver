namespace Weaver.Domain.Exceptions;

public class WorkItemIsBoardScopeException : DomainException
{
    public Guid WorkItemId { get; }

    public Guid BoardId { get; }

    public WorkItemIsBoardScopeException(Guid workItemId, Guid boardId)
        : base(DomainErrorKind.Conflict, $"Work item {workItemId} cannot be deleted because it (or one of its descendants) is the scope of board {boardId}.")
    {
        WorkItemId = workItemId;
        BoardId = boardId;
    }
}
