namespace Weaver.Domain.Exceptions;

public class InvalidWorkItemTagException(Guid workItemId) : DomainException(DomainErrorKind.Validation, $"Work item {workItemId}'s tags must not be empty or blank.")
{
  public Guid WorkItemId { get; } = workItemId;
}
