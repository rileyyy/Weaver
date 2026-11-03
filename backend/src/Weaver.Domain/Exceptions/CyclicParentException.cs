namespace Weaver.Domain.Exceptions;

public class CyclicParentException(Guid workItemId, Guid proposedParentId)
  : DomainException(DomainErrorKind.Conflict, $"Cannot set parent of work item {workItemId} to {proposedParentId}: it would create a cycle.")
{
  public Guid WorkItemId { get; } = workItemId;

  public Guid ProposedParentId { get; } = proposedParentId;
}
