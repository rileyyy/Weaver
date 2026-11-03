namespace Weaver.Domain.Exceptions;

/// <summary>
/// A save collided with a unique index, typically because two requests raced past the same
/// "does it already exist?" check. Services that know which rule the index enforces catch
/// this and throw a more specific exception.
/// </summary>
public class UniqueConstraintViolationException(string? constraintName, Exception innerException) : DomainException(DomainErrorKind.Conflict, "A record with the same unique value already exists.", innerException)
{
  public string? ConstraintName { get; } = constraintName;
}
