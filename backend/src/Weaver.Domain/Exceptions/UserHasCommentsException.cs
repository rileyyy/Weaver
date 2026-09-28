namespace Weaver.Domain.Exceptions;

/// <summary>
/// Comments keep their author, so a user who wrote any can't be deleted outright; they can
/// be deactivated instead.
/// </summary>
public class UserHasCommentsException : DomainException
{
    public UserHasCommentsException(string username, int commentCount)
        : base(
            DomainErrorKind.Conflict,
            $"User '{username}' has written {commentCount} comment(s), so the account can't be deleted; deactivate it instead.")
    {
    }
}
