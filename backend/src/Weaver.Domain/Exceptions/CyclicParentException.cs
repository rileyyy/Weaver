namespace Weaver.Domain.Exceptions;

public class CyclicParentException : DomainException
{
    public Guid WorkItemId { get; }

    public Guid ProposedParentId { get; }

    public CyclicParentException(Guid workItemId, Guid proposedParentId)
        : base(DomainErrorKind.Conflict, $"Cannot set parent of work item {workItemId} to {proposedParentId}: it would create a cycle.")
    {
        WorkItemId = workItemId;
        ProposedParentId = proposedParentId;
    }
}
