namespace Weaver.Domain.Exceptions;

public class InvalidUsernameException(string reason) : DomainException(DomainErrorKind.Validation, reason)
{
}
