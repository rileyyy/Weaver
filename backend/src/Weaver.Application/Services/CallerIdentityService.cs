using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class CallerIdentityService : ICallerIdentityService
{
    private readonly IWeaverDbContext _db;
    private readonly ICallerContext _caller;

    public CallerIdentityService(IWeaverDbContext db, ICallerContext caller)
    {
        _db = db;
        _caller = caller;
    }

    public async Task<CallerIdentity> DescribeAsync(CancellationToken ct = default)
    {
        var caller = _caller.Current;

        // Signed in a moment ago, so a missing account was deleted since: no longer anyone.
        var account = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == caller.AccountId, ct)
            ?? throw new InvalidAccessTokenException();
        var apiKey = caller.ApiKeyId is { } keyId
            ? await _db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Id == keyId, ct)
            : null;

        return new CallerIdentity(account, apiKey);
    }
}
