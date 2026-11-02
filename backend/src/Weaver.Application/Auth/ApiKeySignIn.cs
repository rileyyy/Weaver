using Weaver.Domain;

namespace Weaver.Application.Auth;

/// <summary>The agent account an API key signed in, the key it used and what that key may do.</summary>
public sealed record ApiKeySignIn(Guid AccountId, Guid ApiKeyId, ApiKeyScope Scope);
