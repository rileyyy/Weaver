namespace Weaver.Domain.Exceptions;

public class LookupNameTakenException(string kind, string name) : DomainException(DomainErrorKind.Conflict, $"A {kind.ToLowerInvariant()} named '{name}' already exists.")
{
}
