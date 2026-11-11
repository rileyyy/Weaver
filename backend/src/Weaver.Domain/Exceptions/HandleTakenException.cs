namespace Weaver.Domain.Exceptions;

public class HandleTakenException(string handle, Exception? innerException = null) : DomainException(DomainErrorKind.Conflict, $"Handle '@{handle}' is already taken.", innerException)
{
  public string Handle { get; } = handle;
}
