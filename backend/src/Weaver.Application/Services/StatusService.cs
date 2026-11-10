using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class StatusService(IWeaverDbContext db, IAdminAuthorizer admins, IServerCalendar calendar, ICallerContext caller)
    : SpaceLookupService<Status>(db, admins, caller), IStatusService
{
    private readonly IServerCalendar _calendar = calendar;

    protected override DbSet<Status> Values => Db.Statuses;

    protected override string Kind => nameof(Status);

    protected override int MinimumCount => 1;

    public Task<Status> CreateAsync(
        Guid actorUserId,
        Guid spaceId,
        string name,
        StatusCategory category,
        string color,
        CancellationToken ct = default) =>
        AddAsync(actorUserId, spaceId, order => Status.Create(spaceId, name, order, category, color), ct);

    public Task<Status> UpdateAsync(
        Guid actorUserId,
        Guid id,
        string name,
        StatusCategory category,
        string color,
        CancellationToken ct = default) =>
        EditAsync(actorUserId, id, async status =>
        {
            var wasDone = status.IsDone;
            status.Rename(name);
            status.Recategorize(category, color);
            if (wasDone != status.IsDone)
            {
                await FollowRecategorizationAsync(status, ct);
            }
        }, ct);

    /// <summary>
    /// A status moving into or out of Done completes or reopens every item in it, so each item's
    /// completion date changes as if it had been moved, and is recorded the same way.
    /// </summary>
    private async Task FollowRecategorizationAsync(Status status, CancellationToken ct)
    {
        // Trashed items too: one restored later should come back with the date its status implies.
        var items = await Db.WorkItems.IgnoreQueryFilters().Where(w => w.StatusId == status.Id).ToListAsync(ct);
        if (items.Count == 0)
        {
            return;
        }

        var today = await _calendar.TodayAsync(ct);
        foreach (var item in items)
        {
            History.RecordChanges(item.Id, item.FollowStatusRecategorized(status, today));
        }
    }

    /// <summary>
    /// Moved items go to the bottom of their lane's replacement column, keeping their relative
    /// order. Appending after each cell's last rank means no existing card's rank changes.
    /// </summary>
    protected override async Task MoveWorkItemsAsync(Status deleted, Guid? replacementId, CancellationToken ct)
    {
        // Trashed items too: one restored later mustn't point at a value that's gone.
        var moving = await Db.WorkItems
            .IgnoreQueryFilters()
            .Where(w => w.StatusId == deleted.Id)
            .OrderBy(w => w.Rank).ThenBy(w => w.Number).ThenBy(w => w.Id)
            .ToListAsync(ct);
        if (moving.Count == 0)
        {
            return;
        }

        if (replacementId is not { } replacementStatusId)
        {
            throw new LookupValueInUseException(Kind, deleted.Name, moving.Count);
        }

        var replacement = await Values.FindAsync([replacementStatusId], ct)
            ?? throw new EntityNotFoundException(Kind, replacementStatusId);
        var today = await _calendar.TodayAsync(ct);
        var lastRankByCell = (await Db.WorkItems
                .Where(w => w.StatusId == replacementStatusId)
                .GroupBy(w => new { w.SpaceId, w.ParentId })
                .Select(cell => new { cell.Key.SpaceId, cell.Key.ParentId, LastRank = cell.Max(w => w.Rank) })
                .ToListAsync(ct))
            .ToDictionary(cell => (cell.SpaceId, cell.ParentId), cell => (double?)cell.LastRank);

        // A cell is per Space as well as per parent, so each Space's top-level items are one.
        foreach (var item in moving)
        {
            var cellKey = (item.SpaceId, item.ParentId);
            var rank = RankCalculator.GetRankBetween(lastRankByCell.GetValueOrDefault(cellKey), null);
            History.RecordChanges(item.Id, item.MoveToStatus(replacement, rank, today));
            lastRankByCell[cellKey] = rank;
        }
    }
}
