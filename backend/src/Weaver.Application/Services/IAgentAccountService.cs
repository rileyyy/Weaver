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

    /// <summary>A null or blank <paramref name="handle"/> leaves the agent without one.</summary>
    Task<User> CreateAsync(string name, string? description, string? handle, CancellationToken ct = default);

    /// <summary>
    /// Gives the agent the <c>@</c> handle comments mention it by, or clears it when blank.
    /// Throws <see cref="Weaver.Domain.Exceptions.HandleTakenException"/> if another account has it.
    /// </summary>
    Task<User> SetHandleAsync(Guid agentId, string? handle, CancellationToken ct = default);

    /// <summary>
    /// A deactivated agent's keys stop working but aren't revoked, so reactivating it brings
    /// them back. Otherwise as for <see cref="IUserAdministrationService.SetActiveAsync"/>.
    /// </summary>
    Task<User> SetActiveAsync(Guid agentId, bool isActive, CancellationToken ct = default);

    /// <summary>The agent's keys, revoked ones included, oldest first.</summary>
    Task<IReadOnlyList<ApiKey>> ListKeysAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>Only the hash is stored, so the returned plaintext is the only copy there will be.</summary>
    Task<IssuedApiKey> IssueKeyAsync(Guid agentId, string label, ApiKeyScope scope, CancellationToken ct = default);

    /// <summary>Revoked keys stay listed, so what they did stays attributable; revoking twice is a no-op.</summary>
    Task RevokeKeyAsync(Guid agentId, Guid keyId, CancellationToken ct = default);
}
