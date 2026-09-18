using Microsoft.EntityFrameworkCore;
using Weaver.Domain;

namespace Weaver.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly WeaverDbContext _db;

    public UserService(WeaverDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default) =>
        await _db.Users.OrderBy(u => u.Username).ToListAsync(ct);
}
