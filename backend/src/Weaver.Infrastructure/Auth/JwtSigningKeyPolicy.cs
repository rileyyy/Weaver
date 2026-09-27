using System.Text;

namespace Weaver.Infrastructure.Auth;

/// <summary>
/// Startup checks on <see cref="JwtOptions.SigningKey"/>. Anyone holding the key can mint a
/// token for any user, so a missing, short or publicly known key must stop the API from
/// starting rather than surface at the first login.
/// </summary>
public static class JwtSigningKeyPolicy
{
    /// <summary>The dev key committed in appsettings.Development.json and compose.override.yaml.</summary>
    public const string DevelopmentKey = "insecure-development-only-signing-key-do-not-use-in-production";

    /// <summary>HS256 needs a key of at least 256 bits.</summary>
    public const int MinimumKeyBytes = 32;

    public static void EnsureUsable(string? signingKey, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. Set it via the Jwt__SigningKey environment variable (see .env.example).");
        }

        if (Encoding.UTF8.GetByteCount(signingKey) < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey must be at least {MinimumKeyBytes} bytes. Generate one with `openssl rand -base64 48`.");
        }

        if (!isDevelopment && signingKey == DevelopmentKey)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is the public development key, which must never be used outside Development. " +
                "Generate one with `openssl rand -base64 48`.");
        }
    }
}
