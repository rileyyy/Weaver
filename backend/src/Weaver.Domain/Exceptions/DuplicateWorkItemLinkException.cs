namespace Weaver.Domain.Exceptions;

public class DuplicateWorkItemLinkException(Guid workItemId, Guid linkedWorkItemId) : DomainException(DomainErrorKind.Conflict, $"Work items {workItemId} and {linkedWorkItemId} are already linked.")
{
}
