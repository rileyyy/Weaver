namespace Weaver.Domain.Exceptions;

public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, Guid id)
        : base(DomainErrorKind.NotFound, $"{entityName} {id} was not found.")
    {
    }
}
