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
}
