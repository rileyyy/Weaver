namespace Weaver.Domain.Exceptions;

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityName, Guid id)
        : base($"{entityName} {id} was not found.")
    {
    }
}
