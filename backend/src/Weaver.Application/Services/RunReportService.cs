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
        if (offset < 0)
        {
            throw new DomainValidationException("Offset must not be negative.");
        }

        if (limit is < 1 or > MaxPageSize)
        {
            throw new DomainValidationException($"Limit must be between 1 and {MaxPageSize}.");
        }

        await EnsureAdminAsync(ct);

        // Ordered by the run's last entry id rather than its last timestamp: entries saved in one
        // write share a timestamp, and the id gives every page the same order. One extra run says
        // whether another page follows.
        var groups = await _db.WorkItemHistory
            .Where(e => e.RunId != null && e.AccountId != null)
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
        var keyIds = onPage.Select(r => r.ApiKeyId).OfType<Guid>().Distinct().ToList();
        var keyLabels = await _db.ApiKeys
            .Where(k => keyIds.Contains(k.Id))
            .ToDictionaryAsync(k => k.Id, k => k.Label, ct);

        var runs = onPage
            .Select(r => new RunReport(
                r.AccountId!.Value,
                accountNames[r.AccountId.Value],
                r.ApiKeyId,
                r.ApiKeyId is { } keyId ? keyLabels.GetValueOrDefault(keyId) : null,
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

        var entries = _db.WorkItemHistory.Where(e =>
            e.RunId == run.RunId && e.AccountId == run.AccountId && e.ApiKeyId == run.ApiKeyId);
        var itemIds = await entries
            .GroupBy(e => e.WorkItemId)
            .Select(g => new { WorkItemId = g.Key, FirstEntryId = g.Min(e => e.Id) })
            .OrderBy(i => i.FirstEntryId)
            .Select(i => i.WorkItemId)
            .ToListAsync(ct);
        if (itemIds.Count == 0)
        {
            throw new RunNotFoundException(run.RunId, run.AccountId);
        }

        return await ChangedWorkItemReader.ReadAsync(_db, entries, itemIds, ct);
    }

    private Task EnsureAdminAsync(CancellationToken ct) => _admins.EnsureAdminAsync(_caller.Current.AccountId, ct);
}
