namespace Weaver.Domain.Exceptions;

/// <summary>
/// Something only human accounts have, such as admin rights or a password, was asked of an
/// agent account (ADR-0004).
/// </summary>
public class NotAHumanAccountException(string username, string reason) : DomainException(DomainErrorKind.Conflict, $"'{username}' is an agent account. {reason}")
{
}
