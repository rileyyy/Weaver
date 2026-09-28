namespace Weaver.Domain.Exceptions;

/// <summary>
/// A status is still used by work items and no replacement was given. Every item needs a
/// status, so the items can't simply be cleared as they are for a deleted layer or priority.
/// </summary>
public class LookupValueInUseException : DomainException
{
    public LookupValueInUseException(string kind, string name, int workItemCount)
        : base(
            DomainErrorKind.Conflict,
            $"{kind} '{name}' is used by {workItemCount} work item(s); choose a replacement to move them to before deleting it.")
    {
    }
}
