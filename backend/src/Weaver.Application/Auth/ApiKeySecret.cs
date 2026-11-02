using System.Security.Cryptography;
using System.Text;

namespace Weaver.Application.Auth;

/// <summary>The plaintext form of an API key, and the hash that is all the server keeps of it.</summary>
public static class ApiKeySecret
{
    /// <summary>
    /// Every key starts with this, so the sign-in pipeline can tell a key from an access token
    /// in the same <c>Authorization: Bearer</c> header, and secret scanners can spot a leaked one.
    /// </summary>
    public const string Prefix = "wvr_";

    public static string Generate() =>
        Prefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// A plain SHA-256 rather than a slow password hash: the key is 256 random bits, so there's
    /// nothing to brute-force, and every key-authenticated request has to compute it.
    /// </summary>
    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static bool LooksLikeKey(string? value) => value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
