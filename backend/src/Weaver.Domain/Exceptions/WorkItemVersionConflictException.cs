namespace Weaver.Domain.Exceptions;

public class WorkItemVersionConflictException : Exception
{
    public Guid WorkItemId { get; }

    public WorkItemVersionConflictException(Guid workItemId, Exception? innerException = null)
        : base($"Work item {workItemId} was changed by someone else. Refresh and try again.", innerException)
    {
        WorkItemId = workItemId;
    }
}
