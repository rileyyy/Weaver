namespace Weaver.Domain.Exceptions;

public class InvalidWorkItemScheduleException : Exception
{
    public Guid WorkItemId { get; }

    public InvalidWorkItemScheduleException(Guid workItemId)
        : base($"Work item {workItemId}'s start date must not be after its end date.")
    {
        WorkItemId = workItemId;
    }
}
