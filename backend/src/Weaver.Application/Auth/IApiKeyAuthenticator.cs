namespace Weaver.Application.Auth;

/// <summary>Turns a presented API key into the agent account it belongs to.</summary>
public interface IApiKeyAuthenticator
{
    /// <summary>
    /// Null for an unknown or revoked key, or one whose account is deactivated: all of them
    /// are just "not signed in", so a caller can't learn which.
    /// </summary>
    Task<ApiKeySignIn?> AuthenticateAsync(string key, CancellationToken ct = default);
}
