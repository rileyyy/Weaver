using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class RunReportService(IWeaverDbContext db, IAdminAuthorizer admins, ICallerContext caller) : IRunReportService
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    private readonly IWeaverDbContext _db = db;
    private readonly IAdminAuthorizer _admins = admins;
    private readonly ICallerContext _caller = caller;

    public async Task<RunReportPage> ListAsync(int offset, int limit, CancellationToken ct = default)
    {
        PagingGuard.EnsureValid(offset, limit, MaxPageSize);

        await EnsureAdminAsync(ct);

        // Ordered by the run's last entry id rather than its last timestamp: entries saved in one
        // write share a timestamp, and the id gives every page the same order. One extra run says
        // whether another page follows.
        var groups = await AgentRunEntries()
            .GroupBy(e => new { e.AccountId, e.ApiKeyId, e.RunId })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.ApiKeyId,
                g.Key.RunId,
                FirstChangeAtUtc = g.Min(e => e.CreatedAtUtc),
                LastChangeAtUtc = g.Max(e => e.CreatedAtUtc),
                ChangeCount = g.Count(),
                LastEntryId = g.Max(e => e.Id),
            })
            .OrderByDescending(r => r.LastEntryId)
            .Skip(offset)
            .Take(limit + 1)
            .ToListAsync(ct);
        var onPage = groups.Take(limit).ToList();

        var accountIds = onPage.Select(r => r.AccountId!.Value).Distinct().ToList();
        var accountNames = await _db.Users
            .Where(u => accountIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username, ct);
        var keyIds = onPage.Select(r => r.ApiKeyId!.Value).Distinct().ToList();
        var keyLabels = await _db.ApiKeys
            .Where(k => keyIds.Contains(k.Id))
            .ToDictionaryAsync(k => k.Id, k => k.Label, ct);

        var runs = onPage
            .Select(r => new RunReport(
                r.AccountId!.Value,
                accountNames[r.AccountId.Value],
                r.ApiKeyId!.Value,
                keyLabels[r.ApiKeyId.Value],
                r.RunId!,
                r.FirstChangeAtUtc,
                r.LastChangeAtUtc,
                r.ChangeCount))
            .ToList();
        return new RunReportPage(runs, groups.Count > limit ? offset + limit : null);
    }

    public async Task<IReadOnlyList<ChangedWorkItem>> ListChangesAsync(RunKey run, CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);

        var entries = AgentRunEntries().OfRun(run);
        var itemIds = await entries
            .GroupBy(e => e.WorkItemId)
            .Select(g => new { WorkItemId = g.Key, FirstEntryId = g.Min(e => e.Id) })
            .OrderBy(i => i.FirstEntryId)
            .Select(i => i.WorkItemId)
            .ToListAsync(ct);
        if (itemIds.Count == 0)
        {
            throw new RunNotFoundException(run.RunId, run.AccountId, run.ApiKeyId);
        }

        return await ChangedWorkItemReader.ReadAsync(_db, entries, itemIds, ct);
    }

    /// <summary>
    /// History entries that belong to an agent's run. Only agents sign in with a key, so the key
    /// is what keeps a human's REST write that happened to send a run id out of the reports.
    /// </summary>
    private IQueryable<Domain.WorkItemHistoryEntry> AgentRunEntries() =>
        _db.WorkItemHistory.Where(e => e.RunId != null && e.AccountId != null && e.ApiKeyId != null);

    private Task EnsureAdminAsync(CancellationToken ct) => _admins.EnsureAdminAsync(_caller.Current.AccountId, ct);
}
