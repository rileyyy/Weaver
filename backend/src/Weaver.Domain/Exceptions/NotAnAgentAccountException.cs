namespace Weaver.Domain.Exceptions;

/// <summary>
/// Something only agent accounts have, such as API keys, was asked of a human account
/// (ADR-0004).
/// </summary>
public class NotAnAgentAccountException : DomainException
{
    public NotAnAgentAccountException(string username)
        : base(DomainErrorKind.Conflict, $"'{username}' is a human account. Only agent accounts have API keys.")
    {
    }
}
