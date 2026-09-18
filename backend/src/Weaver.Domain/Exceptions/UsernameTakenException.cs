namespace Weaver.Domain.Exceptions;

public class UsernameTakenException : Exception
{
    public string Username { get; }

    public UsernameTakenException(string username)
        : base($"Username '{username}' is already taken.")
    {
        Username = username;
    }
}
