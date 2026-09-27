namespace Weaver.Domain.Exceptions;

public class DuplicateWorkItemLinkException : DomainException
{
    public DuplicateWorkItemLinkException(Guid workItemId, Guid linkedWorkItemId)
        : base(DomainErrorKind.Conflict, $"Work items {workItemId} and {linkedWorkItemId} are already linked.")
    {
    }
}
