using System.Text.RegularExpressions;
using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// Username and password rules, shared by self-registration and admin-created accounts so
/// the two can't drift.
/// </summary>
public static partial class UserCredentials
{
    public const int MinPasswordLength = 12;
    public const int MaxPasswordLength = 200;

    /// <summary>
    /// The trimmed username and its lowercased form, which is what uniqueness and lookups
    /// compare, since Postgres has no built-in case-insensitive unique index.
    /// </summary>
    public static (string Username, string Normalized) NormalizeUsername(string username)
    {
        var trimmed = username?.Trim() ?? string.Empty;
        if (!UsernamePattern().IsMatch(trimmed))
        {
            throw new InvalidUsernameException(
                "Username must be 3-50 characters and contain only letters, digits, '.', '_', or '-'.");
        }
        return (trimmed, trimmed.ToLowerInvariant());
    }

    public static void ValidatePassword(string password)
    {
        if (password is null || password.Length < MinPasswordLength)
        {
            throw new InvalidPasswordException($"Password must be at least {MinPasswordLength} characters long.");
        }
        if (password.Length > MaxPasswordLength)
        {
            throw new InvalidPasswordException($"Password must be at most {MaxPasswordLength} characters long.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]{3,50}$")]
    private static partial Regex UsernamePattern();
}
