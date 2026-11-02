namespace Weaver.Application.Auth;

/// <summary>The agent account an API key signed in, and the key it used.</summary>
public sealed record ApiKeySignIn(Guid AccountId, Guid ApiKeyId);
