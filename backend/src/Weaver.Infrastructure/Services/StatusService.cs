using Microsoft.EntityFrameworkCore;
using Weaver.Domain;

namespace Weaver.Infrastructure.Services;

public class StatusService : IStatusService
{
    private readonly WeaverDbContext _db;

    public StatusService(WeaverDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Status>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Statuses.OrderBy(s => s.Order).ToListAsync(ct);
}
