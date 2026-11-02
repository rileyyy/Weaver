using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Auth;

/// <summary>
/// Who is making the current request: the account, the API key it signed in with and that
/// key's scope (agents only), and the actor label the agent sent with the write, if any.
/// </summary>
public sealed record Caller(Guid AccountId, Guid? ApiKeyId, string? ActorLabel, ApiKeyScope? ApiKeyScope = null)
{
    /// <summary>
    /// The one rule behind every write, over REST and MCP alike: a read-only key may not make
    /// one. Humans, and agents with a read-write key, may.
    /// </summary>
    public void EnsureCanWrite()
    {
        if (ApiKeyScope == Domain.ApiKeyScope.ReadOnly)
        {
            throw new ReadOnlyApiKeyException();
        }
    }
}
