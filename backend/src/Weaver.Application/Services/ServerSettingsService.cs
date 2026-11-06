using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class ServerSettingsService(IWeaverDbContext db, IAdminAuthorizer admins) : IServerSettingsService
{
    private readonly IWeaverDbContext _db = db;
    private readonly IAdminAuthorizer _admins = admins;

    public Task<ServerSettings> GetAsync(CancellationToken ct = default) =>
        _db.ServerSettings.AsNoTracking().SingleAsync(ct);

    public Task<ServerSettings> SetTimeZoneAsync(Guid actorUserId, string timeZoneId, CancellationToken ct = default) =>
        ChangeAsync(actorUserId, settings => settings.SetTimeZone(timeZoneId), ct);

    public Task<ServerSettings> SetTrashRetentionAsync(Guid actorUserId, int days, CancellationToken ct = default) =>
        ChangeAsync(actorUserId, settings => settings.SetTrashRetention(days), ct);

    private async Task<ServerSettings> ChangeAsync(Guid actorUserId, Action<ServerSettings> change, CancellationToken ct)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var settings = await _db.ServerSettings.SingleAsync(ct);
        change(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }
}
