namespace Weaver.Domain.Exceptions;

public class InvalidWorkItemScheduleException(Guid workItemId) : DomainException(DomainErrorKind.Validation, $"Work item {workItemId}'s start date must not be after its end date.")
{
  public Guid WorkItemId { get; } = workItemId;
}
