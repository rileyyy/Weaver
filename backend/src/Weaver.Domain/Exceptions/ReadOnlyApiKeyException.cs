namespace Weaver.Domain.Exceptions;

/// <summary>An agent signed in with a read-only API key tried to change something (ADR-0004).</summary>
public class ReadOnlyApiKeyException : DomainException
{
    public ReadOnlyApiKeyException()
        : base(
            DomainErrorKind.Forbidden,
            "This API key is read-only, so it can't make changes. An administrator can issue a read-write key.")
    {
    }
}
