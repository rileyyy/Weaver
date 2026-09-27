namespace Weaver.Domain.Exceptions;

public class InvalidPasswordException : DomainException
{
    public InvalidPasswordException(string reason)
        : base(DomainErrorKind.Validation, reason)
    {
    }
}
