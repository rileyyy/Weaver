namespace Weaver.Domain.Exceptions;

/// <summary>An administrator tried to deactivate or delete the account they're signed in with.</summary>
public class OwnAccountChangeException : DomainException
{
    public OwnAccountChangeException()
        : base(DomainErrorKind.Conflict, "You can't deactivate or delete your own account.")
    {
    }
}
