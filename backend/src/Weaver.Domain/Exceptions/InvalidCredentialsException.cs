namespace Weaver.Domain.Exceptions;

/// <summary>
/// Thrown for any login failure — unknown username, wrong password, or a
/// locked account. Deliberately carries no detail about which: a specific
/// message (e.g. "no such user") would let a caller enumerate valid
/// usernames.
/// </summary>
public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid username or password.")
    {
    }
}
