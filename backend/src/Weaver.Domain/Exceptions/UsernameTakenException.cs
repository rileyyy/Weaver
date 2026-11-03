namespace Weaver.Domain.Exceptions;

public class UsernameTakenException(string username, Exception? innerException = null) : DomainException(DomainErrorKind.Conflict, $"Username '{username}' is already taken.", innerException)
{
  public string Username { get; } = username;
}
