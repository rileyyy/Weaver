namespace Weaver.Domain.Exceptions;

public class AdminRequiredException : DomainException
{
    public AdminRequiredException()
        : base(DomainErrorKind.Forbidden, "Only an administrator can do this.")
    {
    }
}
