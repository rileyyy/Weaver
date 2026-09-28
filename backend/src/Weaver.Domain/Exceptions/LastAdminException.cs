namespace Weaver.Domain.Exceptions;

/// <summary>
/// The change would leave no active administrator, and with it no way to manage users or
/// lookups short of editing the database.
/// </summary>
public class LastAdminException : DomainException
{
    public LastAdminException()
        : base(DomainErrorKind.Conflict, "This is the last active administrator; make someone else an administrator first.")
    {
    }
}
