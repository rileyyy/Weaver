namespace Weaver.Domain.Exceptions;

/// <summary>
/// Base for every expected rule violation. Its message is safe to show to the caller.
/// </summary>
public abstract class DomainException(DomainErrorKind kind, string message, Exception? innerException = null)
  : Exception(message, innerException)
{
  public DomainErrorKind Kind { get; } = kind;
}
