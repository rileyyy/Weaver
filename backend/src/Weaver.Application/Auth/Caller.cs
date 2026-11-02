namespace Weaver.Application.Auth;

/// <summary>
/// Who is making the current request: the account, the API key it signed in with (agents
/// only) and the actor label the agent sent with the write, if any.
/// </summary>
public sealed record Caller(Guid AccountId, Guid? ApiKeyId, string? ActorLabel);
