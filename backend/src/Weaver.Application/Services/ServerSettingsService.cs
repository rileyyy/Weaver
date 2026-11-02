using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class ServerSettingsService : IServerSettingsService
{
    private readonly IWeaverDbContext _db;
    private readonly IAdminAuthorizer _admins;

    public ServerSettingsService(IWeaverDbContext db, IAdminAuthorizer admins)
    {
        _db = db;
        _admins = admins;
    }

    public Task<ServerSettings> GetAsync(CancellationToken ct = default) =>
        _db.ServerSettings.AsNoTracking().SingleAsync(ct);

    public async Task<ServerSettings> SetTimeZoneAsync(Guid actorUserId, string timeZoneId, CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var settings = await _db.ServerSettings.SingleAsync(ct);
        settings.SetTimeZone(timeZoneId);
        await _db.SaveChangesAsync(ct);
        return settings;
    }
}
