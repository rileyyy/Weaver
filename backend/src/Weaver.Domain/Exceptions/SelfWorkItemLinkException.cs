namespace Weaver.Domain.Exceptions;

public class SelfWorkItemLinkException : Exception
{
    public SelfWorkItemLinkException(Guid workItemId)
        : base($"Work item {workItemId} cannot be linked to itself.")
    {
    }
}
