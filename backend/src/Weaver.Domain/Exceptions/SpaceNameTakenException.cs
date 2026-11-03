namespace Weaver.Domain.Exceptions;

public class SpaceNameTakenException : DomainException
{
    public SpaceNameTakenException(string name, Exception? innerException = null)
        : base(DomainErrorKind.Conflict, $"A Space named '{name}' already exists.", innerException)
    {
    }
}
