namespace Weaver.Domain.Exceptions;

public class InvalidPasswordException(string reason) : DomainException(DomainErrorKind.Validation, reason)
{
}
