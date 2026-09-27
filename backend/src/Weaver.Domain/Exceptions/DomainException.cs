namespace Weaver.Domain.Exceptions;

/// <summary>
/// Base for every expected rule violation. Its message is safe to show to the caller.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(DomainErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public DomainErrorKind Kind { get; }
}
