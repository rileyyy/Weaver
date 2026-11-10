using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Adds one occurrence of a repeating work item to the context (without saving): a copy of
/// the template and its sub-item tree, each starting in the first status of the template's
/// Space and scheduled on the occurrence date, in that Space. Comments, links and repetitions are not
/// copied. Each copy records its creation in history, attributed to the actor the factory was
/// made for. <c>today</c> is the server's date, the completion date of a copy whose first status
/// is a Done one.
/// </summary>
internal sealed class OccurrenceFactory(IWeaverDbContext db, WorkItemHistoryRecorder history, HistoryActor actor, DateOnly today)
{
    private readonly IWeaverDbContext _db = db;
    private readonly WorkItemHistoryRecorder _history = history;
    private readonly HistoryActor _actor = actor;
    private readonly DateOnly _today = today;
    private readonly Dictionary<(Guid SpaceId, Guid? ParentId, Guid StatusId), double?> _lastRankByCell = new();
    private readonly Dictionary<Guid, Status> _firstStatusBySpace = new();

    public async Task<WorkItem> AddOccurrenceAsync(WorkItem template, DateOnly date, CancellationToken ct)
    {
        var status = await FirstStatusAsync(template.SpaceId, ct);

        var root = await AddCopyAsync(template, template.ParentId, status, date, ct);
        root.MarkAsOccurrenceOf(template.Id, date);

        await AddChildCopiesAsync(template.Id, root.Id, status, date, ct);
        return root;
    }

    private async Task<Status> FirstStatusAsync(Guid spaceId, CancellationToken ct)
    {
        if (!_firstStatusBySpace.TryGetValue(spaceId, out var status))
        {
            status = await _db.Statuses
                .AsNoTracking()
                .Where(s => s.SpaceId == spaceId)
                .OrderBy(s => s.Order)
                .FirstAsync(ct);
            _firstStatusBySpace[spaceId] = status;
        }

        return status;
    }

    private async Task AddChildCopiesAsync(
        Guid templateParentId,
        Guid copyParentId,
        Status status,
        DateOnly date,
        CancellationToken ct)
    {
        // A sub-item that is itself an occurrence of another repeating sub-item is history,
        // not part of the template.
        var children = await _db.WorkItems
            .AsNoTracking()
            .Where(w => w.ParentId == templateParentId && w.RecurrenceSourceId == null)
            .OrderBy(w => w.Rank)
            .ThenBy(w => w.Number)
            .ToListAsync(ct);

        foreach (var child in children)
        {
            var copy = await AddCopyAsync(child, copyParentId, status, date, ct);
            await AddChildCopiesAsync(child.Id, copy.Id, status, date, ct);
        }
    }

    private async Task<WorkItem> AddCopyAsync(
        WorkItem source,
        Guid? parentId,
        Status status,
        DateOnly date,
        CancellationToken ct)
    {
        var rank = await NextRankAtEndAsync(source.SpaceId, parentId, status.Id, ct);
        var copy = source.CopyForOccurrence(parentId, status, rank, date, _today);
        _db.WorkItems.Add(copy);
        _history.RecordCreation(copy, _actor);
        return copy;
    }

    /// <summary>
    /// Appends to the end of the cell. Tracked locally because several copies can land in the
    /// same cell before anything is saved.
    /// </summary>
    private async Task<double> NextRankAtEndAsync(Guid spaceId, Guid? parentId, Guid statusId, CancellationToken ct)
    {
        var cell = (spaceId, parentId, statusId);
        if (!_lastRankByCell.TryGetValue(cell, out var last))
        {
            last = await _db.WorkItems
                .Where(w => w.SpaceId == spaceId && w.ParentId == parentId && w.StatusId == statusId)
                .MaxAsync(w => (double?)w.Rank, ct);
        }

        var rank = RankCalculator.GetRankBetween(last, null);
        _lastRankByCell[cell] = rank;
        return rank;
    }
}
