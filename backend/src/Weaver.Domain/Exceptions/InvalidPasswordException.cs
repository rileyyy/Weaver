namespace Weaver.Domain.Exceptions;

public class InvalidPasswordException : Exception
{
    public InvalidPasswordException(string reason)
        : base(reason)
    {
    }
}
