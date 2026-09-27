using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class StatusService : IStatusService
{
    private readonly IWeaverDbContext _db;

    public StatusService(IWeaverDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Status>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Statuses.OrderBy(s => s.Order).ToListAsync(ct);
}
