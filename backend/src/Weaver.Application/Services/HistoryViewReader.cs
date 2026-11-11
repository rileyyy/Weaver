using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Turns history entries into <see cref="WorkItemHistoryView"/>s: who made each (account, kind,
/// key label) and names for its id values. Shared so every reader of history shows the same.
/// </summary>
internal static class HistoryViewReader
{
    /// <summary>Keeps the order <paramref name="entries"/> already has.</summary>
    public static async Task<List<WorkItemHistoryView>> ReadAsync(
        IWeaverDbContext db,
        IQueryable<WorkItemHistoryEntry> entries,
        CancellationToken ct)
    {
        var rows = await entries
            .AsNoTracking()
            .Select(e => new
            {
                Entry = e,
                AccountName = e.Account == null ? null : e.Account.Username,
                AccountKind = e.Account == null ? (UserKind?)null : e.Account.Kind,
                ApiKeyLabel = e.ApiKey == null ? null : e.ApiKey.Label,
            })
            .ToListAsync(ct);

        var names = await HistoryValueNames.LoadAsync(db, rows.Select(r => r.Entry).ToList(), ct);
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
                r.Entry.RunId,
                r.Entry.Field,
                Parse(r.Entry.OldValue),
                Parse(r.Entry.NewValue),
                names.NameOf(r.Entry.Field, r.Entry.OldValue),
                names.NameOf(r.Entry.Field, r.Entry.NewValue),
                names.FieldNameOf(r.Entry.Field)))
            .ToList();
    }

    private static JsonElement? Parse(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);
}
