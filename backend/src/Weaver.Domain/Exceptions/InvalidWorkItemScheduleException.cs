namespace Weaver.Domain.Exceptions;

public class InvalidWorkItemScheduleException : DomainException
{
    public Guid WorkItemId { get; }

    public InvalidWorkItemScheduleException(Guid workItemId)
        : base(DomainErrorKind.Validation, $"Work item {workItemId}'s start date must not be after its end date.")
    {
        WorkItemId = workItemId;
    }
}
