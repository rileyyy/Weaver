namespace Weaver.Domain.Exceptions;

public class SelfWorkItemLinkException : DomainException
{
    public SelfWorkItemLinkException(Guid workItemId)
        : base(DomainErrorKind.Validation, $"Work item {workItemId} cannot be linked to itself.")
    {
    }
}
