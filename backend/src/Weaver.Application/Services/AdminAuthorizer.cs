using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class AdminAuthorizer : IAdminAuthorizer
{
    private readonly IWeaverDbContext _db;

    public AdminAuthorizer(IWeaverDbContext db)
    {
        _db = db;
    }

    public async Task EnsureAdminAsync(Guid userId, CancellationToken ct = default)
    {
        var isActiveAdmin = await _db.Users.AnyAsync(
            u => u.Id == userId && u.IsAdmin && u.DeactivatedAtUtc == null,
            ct);

        if (!isActiveAdmin)
        {
            throw new AdminRequiredException();
        }
    }
}
