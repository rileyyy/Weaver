namespace Weaver.Domain.Exceptions;

public class EntityNotFoundException(string entityName, Guid id) : DomainException(DomainErrorKind.NotFound, $"{entityName} {id} was not found.")
{
}
