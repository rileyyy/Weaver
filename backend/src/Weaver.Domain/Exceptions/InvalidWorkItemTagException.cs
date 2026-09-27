namespace Weaver.Domain.Exceptions;

public class InvalidWorkItemTagException : DomainException
{
    public Guid WorkItemId { get; }

    public InvalidWorkItemTagException(Guid workItemId)
        : base(DomainErrorKind.Validation, $"Work item {workItemId}'s tags must not be empty or blank.")
    {
        WorkItemId = workItemId;
    }
}
