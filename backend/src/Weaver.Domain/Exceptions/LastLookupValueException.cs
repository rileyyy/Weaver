namespace Weaver.Domain.Exceptions;

/// <summary>Every work item needs a status, so the last one can't be deleted.</summary>
public class LastLookupValueException : DomainException
{
    public LastLookupValueException(string kind)
        : base(DomainErrorKind.Conflict, $"At least one {kind.ToLowerInvariant()} must remain.")
    {
    }
}
