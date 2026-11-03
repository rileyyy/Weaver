using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class AdminAuthorizer(IWeaverDbContext db) : IAdminAuthorizer
{
    private readonly IWeaverDbContext _db = db;

    public async Task EnsureAdminAsync(Guid userId, CancellationToken ct = default)
    {
        var caller = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Kind, u.IsAdmin, IsActive = u.DeactivatedAtUtc == null })
            .FirstOrDefaultAsync(ct);

        // Checked before IsAdmin, so an agent flagged as an admin before GrantAdmin refused
        // agents still can't pass.
        if (caller?.Kind == UserKind.Agent)
        {
            throw new AgentAdministrationException();
        }

        if (caller is not { IsAdmin: true, IsActive: true })
        {
            throw new AdminRequiredException();
        }
    }
}
