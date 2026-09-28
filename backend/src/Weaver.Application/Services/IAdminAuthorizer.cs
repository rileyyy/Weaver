namespace Weaver.Application.Services;

/// <summary>
/// The one check behind every admin operation. It reads the database rather than a token
/// claim, so revoking someone's admin rights takes effect on their next request instead of
/// when their access token expires.
/// </summary>
public interface IAdminAuthorizer
{
    Task EnsureAdminAsync(Guid userId, CancellationToken ct = default);
}
