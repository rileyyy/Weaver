namespace Weaver.Domain.Exceptions;

/// <summary>
/// Thrown when a presented refresh token is unknown, expired, or already
/// revoked (including one already consumed by a prior rotation — reusing a
/// rotated-out token is a signal of a possibly stolen token, not just a
/// stale client).
/// </summary>
public class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException()
        : base("Refresh token is invalid or expired.")
    {
    }
}
