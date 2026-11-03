namespace Weaver.Domain.Exceptions;

public class SpaceNameTakenException(string name, Exception? innerException = null) : DomainException(DomainErrorKind.Conflict, $"A Space named '{name}' already exists.", innerException)
{
}
