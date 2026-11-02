using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Names for the ids history values hold (a status, layer, priority, user or another work
/// item), so readers don't each have to look them up. Looked up when read, so an id whose
/// row is gone since has no name and readers fall back to the raw value.
/// </summary>
internal sealed class HistoryValueNames
{
    private enum Kind
    {
        Status,
        Layer,
        Priority,
        User,
        WorkItem,
    }

    private static readonly Dictionary<string, Kind> KindByField = new()
    {
        [WorkItemField.StatusId] = Kind.Status,
        [WorkItemField.LayerId] = Kind.Layer,
        [WorkItemField.PriorityId] = Kind.Priority,
        [WorkItemField.AssignedToUserId] = Kind.User,
        [WorkItemField.ParentId] = Kind.WorkItem,
        [WorkItemHistoryField.Link] = Kind.WorkItem,
    };

    private readonly Dictionary<(Kind, Guid), string> _names;

    private HistoryValueNames(Dictionary<(Kind, Guid), string> names)
    {
        _names = names;
    }

    public static async Task<HistoryValueNames> LoadAsync(
        IWeaverDbContext db,
        IReadOnlyCollection<WorkItemHistoryEntry> entries,
        CancellationToken ct)
    {
        var idsByKind = entries
            .Where(e => KindByField.ContainsKey(e.Field))
            .SelectMany(e => new[] { e.OldValue, e.NewValue }.Select(v => (Kind: KindByField[e.Field], Id: IdIn(v))))
            .Where(x => x.Id is not null)
            .GroupBy(x => x.Kind, x => x.Id!.Value)
            .ToDictionary(g => g.Key, g => g.Distinct().ToList());

        var names = new Dictionary<(Kind, Guid), string>();
        foreach (var (kind, ids) in idsByKind)
        {
            foreach (var (id, name) in await LoadNamesAsync(db, kind, ids, ct))
            {
                names[(kind, id)] = name;
            }
        }

        return new HistoryValueNames(names);
    }

    public string? NameOf(string field, string? valueJson) =>
        KindByField.TryGetValue(field, out var kind) && IdIn(valueJson) is { } id
            ? _names.GetValueOrDefault((kind, id))
            : null;

    private static Guid? IdIn(string? valueJson)
    {
        if (valueJson is null)
        {
            return null;
        }

        var value = JsonSerializer.Deserialize<JsonElement>(valueJson);
        return value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id) ? id : null;
    }

    private static async Task<List<(Guid Id, string Name)>> LoadNamesAsync(
        IWeaverDbContext db,
        Kind kind,
        List<Guid> ids,
        CancellationToken ct) =>
        kind switch
        {
            Kind.Status => (await db.Statuses.Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, s.Name }).ToListAsync(ct))
                .Select(x => (x.Id, x.Name)).ToList(),
            Kind.Layer => (await db.WorkItemLayers.Where(l => ids.Contains(l.Id)).Select(l => new { l.Id, l.Name }).ToListAsync(ct))
                .Select(x => (x.Id, x.Name)).ToList(),
            Kind.Priority => (await db.WorkItemPriorities.Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.Name }).ToListAsync(ct))
                .Select(x => (x.Id, x.Name)).ToList(),
            Kind.User => (await db.Users.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.Username }).ToListAsync(ct))
                .Select(x => (x.Id, x.Username)).ToList(),
            _ => (await db.WorkItems.Where(w => ids.Contains(w.Id)).Select(w => new { w.Id, w.Number, w.Title }).ToListAsync(ct))
                .Select(x => (x.Id, $"#{x.Number} {x.Title}")).ToList(),
        };
}
