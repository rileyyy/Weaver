namespace Weaver.Domain.Exceptions;

public class WorkItemVersionConflictException(Guid workItemId, Exception? innerException = null) : DomainException(DomainErrorKind.Conflict, $"Work item {workItemId} was changed by someone else. Refresh and try again.", innerException)
{
  public Guid WorkItemId { get; } = workItemId;
}
