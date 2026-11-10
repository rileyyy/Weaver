namespace Weaver.Domain.Exceptions;

public class ViewNameTakenException(string name, Exception? innerException = null) : DomainException(DomainErrorKind.Conflict, $"A View named '{name}' already exists.", innerException)
{
}
