namespace Weaver.Domain.Exceptions;

/// <summary>
/// Reparenting never crosses a Space boundary (ADR-0001): moving an item to another Space has
/// to remap its configuration, so it's a separate operation.
/// </summary>
public class CrossSpaceParentException : DomainException
{
    public Guid WorkItemId { get; }

    public Guid ProposedParentId { get; }

    public CrossSpaceParentException(Guid workItemId, Guid proposedParentId)
        : base(DomainErrorKind.Conflict, $"Cannot set parent of work item {workItemId} to {proposedParentId}: it is in another Space.")
    {
        WorkItemId = workItemId;
        ProposedParentId = proposedParentId;
    }
}
