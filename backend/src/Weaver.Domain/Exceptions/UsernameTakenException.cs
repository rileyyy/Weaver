namespace Weaver.Domain.Exceptions;

public class UsernameTakenException : DomainException
{
    public string Username { get; }

    public UsernameTakenException(string username, Exception? innerException = null)
        : base(DomainErrorKind.Conflict, $"Username '{username}' is already taken.", innerException)
    {
        Username = username;
    }
}
