namespace Weaver.Domain.Exceptions;

public class InvalidUsernameException : Exception
{
    public InvalidUsernameException(string reason)
        : base(reason)
    {
    }
}
