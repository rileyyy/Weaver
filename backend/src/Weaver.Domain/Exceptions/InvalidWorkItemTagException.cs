namespace Weaver.Domain.Exceptions;

public class InvalidWorkItemTagException : Exception
{
    public Guid WorkItemId { get; }

    public InvalidWorkItemTagException(Guid workItemId)
        : base($"Work item {workItemId}'s tags must not be empty or blank.")
    {
        WorkItemId = workItemId;
    }
}
