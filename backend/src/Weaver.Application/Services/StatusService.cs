using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class StatusService : LookupService<Status>, IStatusService
{
    public StatusService(IWeaverDbContext db, IAdminAuthorizer admins) : base(db, admins)
    {
    }

    protected override DbSet<Status> Values => Db.Statuses;

    protected override string Kind => nameof(Status);

    protected override int MinimumCount => 1;

    public Task<Status> CreateAsync(
        Guid actorUserId,
        string name,
        StatusCategory category,
        string color,
        CancellationToken ct = default) =>
        AddAsync(actorUserId, order => Status.Create(name, order, category, color), ct);

    public Task<Status> UpdateAsync(
        Guid actorUserId,
        Guid id,
        string name,
        StatusCategory category,
        string color,
        CancellationToken ct = default) =>
        EditAsync(actorUserId, id, status =>
        {
            status.Rename(name);
            status.Recategorize(category, color);
        }, ct);

    /// <summary>
    /// Moved items go to the bottom of their lane's replacement column, keeping their relative
    /// order. Appending after each cell's last rank means no existing card's rank changes.
    /// </summary>
    protected override async Task MoveWorkItemsAsync(Status deleted, Guid? replacementId, CancellationToken ct)
    {
        var moving = await Db.WorkItems
            .Where(w => w.StatusId == deleted.Id)
            .OrderBy(w => w.Rank).ThenBy(w => w.Number).ThenBy(w => w.Id)
            .ToListAsync(ct);
        if (moving.Count == 0)
        {
            return;
        }

        if (replacementId is not { } replacement)
        {
            throw new LookupValueInUseException(Kind, deleted.Name, moving.Count);
        }

        var lastRankByParent = await Db.WorkItems
            .Where(w => w.StatusId == replacement)
            .GroupBy(w => w.ParentId)
            .Select(cell => new { ParentId = cell.Key, LastRank = cell.Max(w => w.Rank) })
            .ToDictionaryAsync(cell => cell.ParentId ?? Guid.Empty, cell => (double?)cell.LastRank, ct);

        // Top-level items (no parent) form one cell; Guid.Empty stands in for the null key.
        foreach (var item in moving)
        {
            var cellKey = item.ParentId ?? Guid.Empty;
            var rank = RankCalculator.GetRankBetween(lastRankByParent.GetValueOrDefault(cellKey), null);
            item.MoveToStatus(replacement, rank);
            lastRankByParent[cellKey] = rank;
        }
    }
}
