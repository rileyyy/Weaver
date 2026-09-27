namespace Weaver.Domain.Exceptions;

public class WorkItemHasChildrenException : DomainException
{
    public Guid WorkItemId { get; }

    public WorkItemHasChildrenException(Guid workItemId)
        : base(DomainErrorKind.Conflict, $"Work item {workItemId} has children; pass cascade=true or reparent its children before deleting it.")
    {
        WorkItemId = workItemId;
    }
}
