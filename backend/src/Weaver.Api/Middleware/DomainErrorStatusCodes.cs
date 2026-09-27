using Weaver.Domain.Exceptions;

namespace Weaver.Api.Middleware;

/// <summary>The one place a <see cref="DomainErrorKind"/> becomes an HTTP status.</summary>
public static class DomainErrorStatusCodes
{
    public static int For(DomainErrorKind kind) => kind switch
    {
        DomainErrorKind.NotFound => StatusCodes.Status404NotFound,
        DomainErrorKind.Validation => StatusCodes.Status400BadRequest,
        DomainErrorKind.Conflict => StatusCodes.Status409Conflict,
        DomainErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        DomainErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No HTTP status for this kind."),
    };
}
