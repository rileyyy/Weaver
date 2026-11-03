namespace Weaver.Domain.Exceptions;

public class SelfWorkItemLinkException(Guid workItemId) : DomainException(DomainErrorKind.Validation, $"Work item {workItemId} cannot be linked to itself.")
{
}
