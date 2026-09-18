namespace Weaver.Domain.Exceptions;

public class DuplicateWorkItemLinkException : Exception
{
    public DuplicateWorkItemLinkException(Guid workItemId, Guid linkedWorkItemId)
        : base($"Work items {workItemId} and {linkedWorkItemId} are already linked.")
    {
    }
}
