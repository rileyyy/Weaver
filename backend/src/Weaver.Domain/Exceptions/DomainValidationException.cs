namespace Weaver.Domain.Exceptions;

/// <summary>
/// Input that breaks a field rule (blank, too long, too many). Maps to 400 in
/// both REST and MCP, so a new rule never needs a new exception mapping.
/// </summary>
public class DomainValidationException : Exception
{
    public DomainValidationException(string message)
        : base(message)
    {
    }
}
