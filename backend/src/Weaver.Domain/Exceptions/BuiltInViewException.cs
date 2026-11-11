namespace Weaver.Domain.Exceptions;

/// <summary>Overview is built in, so it can't be changed or deleted.</summary>
public class BuiltInViewException(string name) : DomainException(DomainErrorKind.Validation, $"'{name}' is built in and can't be changed or deleted.")
{
}
