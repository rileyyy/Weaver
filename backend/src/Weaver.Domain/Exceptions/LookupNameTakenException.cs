namespace Weaver.Domain.Exceptions;

public class LookupNameTakenException : DomainException
{
    public LookupNameTakenException(string kind, string name)
        : base(DomainErrorKind.Conflict, $"A {kind.ToLowerInvariant()} named '{name}' already exists.")
    {
    }
}
