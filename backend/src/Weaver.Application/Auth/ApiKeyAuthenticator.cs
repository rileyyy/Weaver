using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Auth;

public class ApiKeyAuthenticator(IWeaverDbContext db, TimeProvider clock) : IApiKeyAuthenticator
{
    private readonly IWeaverDbContext _db = db;
    private readonly TimeProvider _clock = clock;

    public async Task<ApiKeySignIn?> AuthenticateAsync(string key, CancellationToken ct = default)
    {
        var keyHash = ApiKeySecret.Hash(key);
        var apiKey = await _db.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);

        if (apiKey is null || apiKey.IsRevoked || apiKey.User is not { IsActive: true, Kind: UserKind.Agent })
        {
            return null;
        }

        if (apiKey.RecordUse(_clock.GetUtcNow()))
        {
            await _db.SaveChangesAsync(ct);
        }

        return new ApiKeySignIn(apiKey.UserId, apiKey.Id, apiKey.Scope);
    }
}
