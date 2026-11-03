namespace Weaver.Domain.Exceptions;

public class WorkItemHasChildrenException(Guid workItemId) : DomainException(DomainErrorKind.Conflict, $"Work item {workItemId} has children; pass cascade=true or reparent its children before deleting it.")
{
  public Guid WorkItemId { get; } = workItemId;
}
