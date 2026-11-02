using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>The current caller's account, and the API key it signed in with if it used one.</summary>
public sealed record CallerIdentity(User Account, ApiKey? ApiKey);
