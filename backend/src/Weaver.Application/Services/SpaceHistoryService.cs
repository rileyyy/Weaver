using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class SpaceHistoryService(IWeaverDbContext db) : ISpaceHistoryService
{
    private readonly IWeaverDbContext _db = db;

    public async Task<IReadOnlyList<SpaceHistoryView>> ListForSpaceAsync(Guid spaceId, CancellationToken ct = default)
    {
        if (!await _db.Spaces.AnyAsync(s => s.Id == spaceId, ct))
        {
            throw new EntityNotFoundException(nameof(Space), spaceId);
        }

        var rows = await (
            from e in _db.SpaceHistory.AsNoTracking()
            where e.SpaceId == spaceId
            join account in _db.Users on e.AccountId equals account.Id into accounts
            from account in accounts.DefaultIfEmpty()
            join key in _db.ApiKeys on e.ApiKeyId equals key.Id into keys
            from key in keys.DefaultIfEmpty()
            orderby e.Id descending
            select new
            {
                Entry = e,
                AccountName = account == null ? null : account.Username,
                AccountKind = account == null ? (UserKind?)null : account.Kind,
                ApiKeyLabel = key == null ? null : key.Label,
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new SpaceHistoryView(
                r.Entry.Id,
                r.Entry.SpaceId,
                r.Entry.CreatedAtUtc,
                r.Entry.AccountId,
                r.AccountName,
                r.AccountKind,
                r.Entry.ApiKeyId,
                r.ApiKeyLabel,
                r.Entry.ActorLabel,
                r.Entry.Field,
                Parse(r.Entry.OldValue),
                Parse(r.Entry.NewValue)))
            .ToList();
    }

    private static JsonElement? Parse(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);
}
