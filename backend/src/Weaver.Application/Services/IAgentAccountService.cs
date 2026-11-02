using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Agent accounts and their API keys (ADR-0004). Every operation is refused unless the caller
/// is an active admin.
/// </summary>
public interface IAgentAccountService
{
    /// <summary>Every agent account, including deactivated ones.</summary>
    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default);

    Task<User> CreateAsync(string name, string? description, CancellationToken ct = default);

    /// <summary>The agent's keys, revoked ones included, oldest first.</summary>
    Task<IReadOnlyList<ApiKey>> ListKeysAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>Only the hash is stored, so the returned plaintext is the only copy there will be.</summary>
    Task<IssuedApiKey> IssueKeyAsync(Guid agentId, string label, ApiKeyScope scope, CancellationToken ct = default);
}
