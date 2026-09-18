namespace Weaver.Domain.Exceptions;

public class WorkItemHasChildrenException : Exception
{
    public Guid WorkItemId { get; }

    public WorkItemHasChildrenException(Guid workItemId)
        : base($"Work item {workItemId} has children; pass cascade=true or reparent its children before deleting it.")
    {
        WorkItemId = workItemId;
    }
}
