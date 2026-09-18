namespace Weaver.Domain.Exceptions;

public class CyclicParentException : Exception
{
    public Guid WorkItemId { get; }

    public Guid ProposedParentId { get; }

    public CyclicParentException(Guid workItemId, Guid proposedParentId)
        : base($"Cannot set parent of work item {workItemId} to {proposedParentId}: it would create a cycle.")
    {
        WorkItemId = workItemId;
        ProposedParentId = proposedParentId;
    }
}
