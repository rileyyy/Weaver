using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemHistoryService : IWorkItemHistoryService
{
    private readonly IWeaverDbContext _db;

    public WorkItemHistoryService(IWeaverDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WorkItemHistoryView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var rows = await _db.WorkItemHistory
            .AsNoTracking()
            .Where(e => e.WorkItemId == workItemId)
            .OrderByDescending(e => e.Id)
            .Select(e => new
            {
                Entry = e,
                AccountName = e.Account == null ? null : e.Account.Username,
                AccountKind = e.Account == null ? (UserKind?)null : e.Account.Kind,
                ApiKeyLabel = e.ApiKey == null ? null : e.ApiKey.Label,
            })
            .ToListAsync(ct);

        if (rows.Count == 0 && !await _db.WorkItems.AnyAsync(w => w.Id == workItemId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), workItemId);
        }

        var names = await HistoryValueNames.LoadAsync(_db, rows.Select(r => r.Entry).ToList(), ct);
        return rows
            .Select(r => new WorkItemHistoryView(
                r.Entry.Id,
                r.Entry.WorkItemId,
                r.Entry.CreatedAtUtc,
                r.Entry.AccountId,
                r.AccountName,
                r.AccountKind,
                r.Entry.ApiKeyId,
                r.ApiKeyLabel,
                r.Entry.ActorLabel,
                r.Entry.Field,
                Parse(r.Entry.OldValue),
                Parse(r.Entry.NewValue),
                names.NameOf(r.Entry.Field, r.Entry.OldValue),
                names.NameOf(r.Entry.Field, r.Entry.NewValue)))
            .ToList();
    }

    private static JsonElement? Parse(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);
}
