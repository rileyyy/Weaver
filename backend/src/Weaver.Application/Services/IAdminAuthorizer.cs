namespace Weaver.Application.Services;

/// <summary>
/// The one check behind every admin operation. It reads the database rather than a token
/// claim, so revoking someone's admin rights takes effect on their next request instead of
/// when their access token expires. Agent accounts never pass it, so no agent can change admin
/// data over REST or MCP, whatever its key's scope (ADR-0004).
/// </summary>
public interface IAdminAuthorizer
{
    Task EnsureAdminAsync(Guid userId, CancellationToken ct = default);
}
