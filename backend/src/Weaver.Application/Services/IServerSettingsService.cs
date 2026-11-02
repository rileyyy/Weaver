using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// The server-wide settings. Anyone signed in can read them; each change takes the acting
/// user's id and refuses anyone who isn't an active admin.
/// </summary>
public interface IServerSettingsService
{
    Task<ServerSettings> GetAsync(CancellationToken ct = default);

    /// <summary>Rejects anything that isn't an IANA time zone id.</summary>
    Task<ServerSettings> SetTimeZoneAsync(Guid actorUserId, string timeZoneId, CancellationToken ct = default);
}
