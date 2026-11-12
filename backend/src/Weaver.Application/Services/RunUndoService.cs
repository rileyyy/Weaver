using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class RunUndoService(
    IWeaverDbContext db,
    IAdminAuthorizer admins,
    ICallerContext caller,
    IWorkItemService workItems,
    IWorkItemLinkService links,
    IWorkItemUrlLinkService urlLinks,
    IWorkItemTrashService trash) : IRunUndoService
{
    private readonly IWeaverDbContext _db = db;
    private readonly IAdminAuthorizer _admins = admins;
    private readonly ICallerContext _caller = caller;
    private readonly IWorkItemService _workItems = workItems;
    private readonly IWorkItemLinkService _links = links;
    private readonly IWorkItemUrlLinkService _urlLinks = urlLinks;
    private readonly IWorkItemTrashService _trash = trash;

    public async Task<RunUndoResult> UndoAsync(RunKey run, CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(_caller.Current.AccountId, ct);

        var runEntries = await _db.WorkItemHistory
            .AsNoTracking()
            .OfRun(run)
            .OrderBy(e => e.Id)
            .ToListAsync(ct);
        if (runEntries.Count == 0)
        {
            throw new RunNotFoundException(run.RunId, run.AccountId, run.ApiKeyId);
        }

        var items = runEntries
            .GroupBy(e => e.WorkItemId)
            .Select(g => new RunItemEntries(g.Key, g.ToList()))
            .ToList();
        var operation = new RunUndoOperation(
            _db, _workItems, _links, _urlLinks, _trash, items, await LaterChangesAsync(run, runEntries, ct));

        // Each service saves as it goes; the transaction makes the undo all or nothing.
        await using (var transaction = await _db.BeginTransactionAsync(ct))
        {
            await operation.RunAsync(ct);
            await transaction.CommitAsync(ct);
        }

        return new RunUndoResult(await NameAsync(operation.Outcomes, ct));
    }

    /// <summary>
    /// Every entry on the run's items after its first one that isn't the run's own: the changes an
    /// undo must not overwrite. Read before the undo writes any of its own.
    /// </summary>
    private async Task<LaterChanges> LaterChangesAsync(
        RunKey run,
        IReadOnlyList<WorkItemHistoryEntry> runEntries,
        CancellationToken ct)
    {
        var itemIds = runEntries.Select(e => e.WorkItemId).Distinct().ToList();
        var firstId = runEntries[0].Id;
        var entries = await _db.WorkItemHistory
            .AsNoTracking()
            .Where(e => itemIds.Contains(e.WorkItemId) && e.Id > firstId)
            .NotOfRun(run)
            .ToListAsync(ct);
        return new LaterChanges(entries);
    }

    /// <summary>Trashed items keep their row; a purged item's title comes from its history.</summary>
    private async Task<IReadOnlyList<RunUndoChange>> NameAsync(
        IReadOnlyList<RunEntryOutcome> outcomes,
        CancellationToken ct)
    {
        var itemIds = outcomes.Select(o => o.WorkItemId).Distinct().ToList();
        var rows = await _db.WorkItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => itemIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => new { w.Number, w.Title }, ct);
        var purgedIds = itemIds.Where(id => !rows.ContainsKey(id)).ToList();
        var traces = purgedIds.Count == 0 ? [] : await DeletedItemTrace.LoadAsync(_db, purgedIds, ct);

        return outcomes
            .Select(o => rows.TryGetValue(o.WorkItemId, out var row)
                ? new RunUndoChange(o.WorkItemId, row.Number, row.Title, o.Field, o.Result)
                : new RunUndoChange(
                    o.WorkItemId, null, traces.GetValueOrDefault(o.WorkItemId)?.Title ?? string.Empty, o.Field, o.Result))
            .ToList();
    }
}
