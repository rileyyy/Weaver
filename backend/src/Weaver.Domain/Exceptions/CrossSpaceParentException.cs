namespace Weaver.Domain.Exceptions;

/// <summary>
/// Reparenting never crosses a Space boundary (ADR-0001): moving an item to another Space has
/// to remap its configuration, so it's a separate operation.
/// </summary>
public class CrossSpaceParentException(Guid workItemId, Guid proposedParentId)
: DomainException(DomainErrorKind.Conflict, $"Cannot set parent of work item {workItemId} to {proposedParentId}: it is in another Space.")
{
  public Guid WorkItemId { get; } = workItemId;

  public Guid ProposedParentId { get; } = proposedParentId;
}
