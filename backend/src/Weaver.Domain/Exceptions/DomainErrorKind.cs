namespace Weaver.Domain.Exceptions;

/// <summary>
/// What kind of rule a <see cref="DomainException"/> reports. Transports map each kind to
/// their own error shape (an HTTP status, an MCP error) in one place, so a new exception
/// needs no transport changes.
/// </summary>
public enum DomainErrorKind
{
    NotFound,
    Validation,
    Conflict,
    Unauthorized,
    Forbidden,
}
