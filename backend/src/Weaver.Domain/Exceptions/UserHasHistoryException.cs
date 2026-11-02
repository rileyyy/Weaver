namespace Weaver.Domain.Exceptions;

/// <summary>
/// History keeps who made each change, so an account that changed any work item can't be
/// deleted outright; it can be deactivated instead.
/// </summary>
public class UserHasHistoryException : DomainException
{
    public UserHasHistoryException(string username)
        : base(
            DomainErrorKind.Conflict,
            $"User '{username}' has changed work items, and the history names them, so the account can't be deleted; deactivate it instead.")
    {
    }
}
