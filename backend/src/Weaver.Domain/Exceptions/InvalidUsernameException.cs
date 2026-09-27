namespace Weaver.Domain.Exceptions;

public class InvalidUsernameException : DomainException
{
    public InvalidUsernameException(string reason)
        : base(DomainErrorKind.Validation, reason)
    {
    }
}
