using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class StatusService : LookupService<Status>, IStatusService
{
    public StatusService(IWeaverDbContext db, IAdminAuthorizer admins, ICallerContext caller)
        : base(db, admins, caller)
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

        var lastRankByCell = (await Db.WorkItems
                .Where(w => w.StatusId == replacement)
                .GroupBy(w => new { w.SpaceId, w.ParentId })
                .Select(cell => new { cell.Key.SpaceId, cell.Key.ParentId, LastRank = cell.Max(w => w.Rank) })
                .ToListAsync(ct))
            .ToDictionary(cell => (cell.SpaceId, cell.ParentId), cell => (double?)cell.LastRank);

        // A cell is per Space as well as per parent, so each Space's top-level items are one.
        foreach (var item in moving)
        {
            var cellKey = (item.SpaceId, item.ParentId);
            var rank = RankCalculator.GetRankBetween(lastRankByCell.GetValueOrDefault(cellKey), null);
            History.RecordChanges(item.Id, item.MoveToStatus(replacement, rank));
            lastRankByCell[cellKey] = rank;
        }
    }
}
