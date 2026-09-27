using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Adds one occurrence of a repeating work item to the context (without saving): a copy of
/// the template and its sub-item tree, each starting in the first status and scheduled on
/// the occurrence date. Comments, links and repetitions are not copied.
/// </summary>
internal sealed class OccurrenceFactory
{
    private readonly IWeaverDbContext _db;
    private readonly Dictionary<(Guid? ParentId, Guid StatusId), double?> _lastRankByCell = new();
    private Guid? _firstStatusId;

    public OccurrenceFactory(IWeaverDbContext db)
    {
        _db = db;
    }

    public async Task<WorkItem> AddOccurrenceAsync(WorkItem template, DateOnly date, CancellationToken ct)
    {
        var statusId = _firstStatusId ??= await _db.Statuses.OrderBy(s => s.Order).Select(s => s.Id).FirstAsync(ct);

        var root = await AddCopyAsync(template, template.ParentId, statusId, date, ct);
        root.MarkAsOccurrenceOf(template.Id, date);

        await AddChildCopiesAsync(template.Id, root.Id, statusId, date, ct);
        return root;
    }

    private async Task AddChildCopiesAsync(
        Guid templateParentId,
        Guid copyParentId,
        Guid statusId,
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
            var copy = await AddCopyAsync(child, copyParentId, statusId, date, ct);
            await AddChildCopiesAsync(child.Id, copy.Id, statusId, date, ct);
        }
    }

    private async Task<WorkItem> AddCopyAsync(
        WorkItem source,
        Guid? parentId,
        Guid statusId,
        DateOnly date,
        CancellationToken ct)
    {
        var copy = source.CopyForOccurrence(parentId, statusId, await NextRankAtEndAsync(parentId, statusId, ct), date);
        _db.WorkItems.Add(copy);
        return copy;
    }

    /// <summary>
    /// Appends to the end of the cell. Tracked locally because several copies can land in the
    /// same cell before anything is saved.
    /// </summary>
    private async Task<double> NextRankAtEndAsync(Guid? parentId, Guid statusId, CancellationToken ct)
    {
        var cell = (parentId, statusId);
        if (!_lastRankByCell.TryGetValue(cell, out var last))
        {
            last = await _db.WorkItems
                .Where(w => w.ParentId == parentId && w.StatusId == statusId)
                .MaxAsync(w => (double?)w.Rank, ct);
        }

        var rank = RankCalculator.GetRankBetween(last, null);
        _lastRankByCell[cell] = rank;
        return rank;
    }
}
