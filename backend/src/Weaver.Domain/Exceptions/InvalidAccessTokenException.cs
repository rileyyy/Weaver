namespace Weaver.Domain.Exceptions;

public class InvalidAccessTokenException : DomainException
{
    public InvalidAccessTokenException()
        : base(DomainErrorKind.Unauthorized, "The access token does not identify a user.")
    {
    }
}
