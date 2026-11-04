using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Names for the ids history values hold (a Space, status, type, priority, user or another
/// work item, or a custom field's option or user), and for the custom fields themselves, so
/// readers don't each have to look them up. Looked up when read, so an id whose row is gone
/// since has no name and readers fall back to the raw value.
/// </summary>
internal sealed class HistoryValueNames
{
    private enum Kind
    {
        Space,
        Status,
        Type,
        Priority,
        User,
        WorkItem,
    }

    private static readonly Dictionary<string, Kind> KindByField = new()
    {
        [WorkItemField.SpaceId] = Kind.Space,
        [WorkItemField.StatusId] = Kind.Status,
        [WorkItemField.TypeId] = Kind.Type,
        [WorkItemField.PriorityId] = Kind.Priority,
        [WorkItemField.AssignedToUserId] = Kind.User,
        [WorkItemField.ParentId] = Kind.WorkItem,
        [WorkItemHistoryField.Link] = Kind.WorkItem,
    };

    private readonly Dictionary<(Kind, Guid), string> _names;
    private readonly Dictionary<Guid, CustomFieldDefinition> _customFields;

    private HistoryValueNames(Dictionary<(Kind, Guid), string> names, Dictionary<Guid, CustomFieldDefinition> customFields)
    {
        _names = names;
        _customFields = customFields;
    }

    public static async Task<HistoryValueNames> LoadAsync(
        IWeaverDbContext db,
        IReadOnlyCollection<WorkItemHistoryEntry> entries,
        CancellationToken ct)
    {
        var customFieldIds = entries
            .Select(e => WorkItemField.CustomFieldId(e.Field))
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var customFields = customFieldIds.Count == 0
            ? []
            : await db.CustomFields.AsNoTracking().Where(f => customFieldIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);

        var kindByField = new Dictionary<string, Kind>(KindByField);
        foreach (var field in customFields.Values.Where(f => f.Kind == CustomFieldKind.User))
        {
            kindByField[field.ChangeName] = Kind.User;
        }

        var idsByKind = entries
            .Where(e => kindByField.ContainsKey(e.Field))
            .SelectMany(e => new[] { e.OldValue, e.NewValue }.Select(v => (Kind: kindByField[e.Field], Id: IdIn(v))))
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

        return new HistoryValueNames(names, customFields);
    }

    public string? NameOf(string field, string? valueJson)
    {
        if (CustomFieldOf(field) is { } customField)
        {
            return CustomValueName(customField, valueJson);
        }

        return KindByField.TryGetValue(field, out var kind) && IdIn(valueJson) is { } id
            ? _names.GetValueOrDefault((kind, id))
            : null;
    }

    /// <summary>A custom field's current name; null for every other field, or a field since removed.</summary>
    public string? FieldNameOf(string field) => CustomFieldOf(field)?.Name;

    private CustomFieldDefinition? CustomFieldOf(string field) =>
        WorkItemField.CustomFieldId(field) is { } id ? _customFields.GetValueOrDefault(id) : null;

    /// <summary>Options removed since have no name, and are left out of a multi-select's.</summary>
    private string? CustomValueName(CustomFieldDefinition field, string? valueJson)
    {
        if (valueJson is null)
        {
            return null;
        }

        switch (field.Kind)
        {
            case CustomFieldKind.User:
                return IdIn(valueJson) is { } userId ? _names.GetValueOrDefault((Kind.User, userId)) : null;
            case CustomFieldKind.SingleSelect:
                return OptionName(field, IdIn(valueJson));
            case CustomFieldKind.MultiSelect:
                var names = OptionIdsIn(valueJson).Select(id => OptionName(field, id)).OfType<string>().ToList();
                return names.Count == 0 ? null : string.Join(", ", names);
            default:
                return null;
        }
    }

    private static IEnumerable<Guid?> OptionIdsIn(string valueJson)
    {
        var value = JsonSerializer.Deserialize<JsonElement>(valueJson);
        return value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(e => e.TryGetGuid(out var id) ? id : (Guid?)null).ToList()
            : [];
    }

    private static string? OptionName(CustomFieldDefinition field, Guid? optionId) =>
        field.Options.FirstOrDefault(o => o.Id == optionId)?.Name;

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
            Kind.Space => (await db.Spaces.Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, s.Name }).ToListAsync(ct))
                .Select(x => (x.Id, x.Name)).ToList(),
            Kind.Status => (await db.Statuses.Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, s.Name }).ToListAsync(ct))
                .Select(x => (x.Id, x.Name)).ToList(),
            Kind.Type => (await db.WorkItemTypes.Where(t => ids.Contains(t.Id)).Select(t => new { t.Id, t.Name }).ToListAsync(ct))
                .Select(x => (x.Id, x.Name)).ToList(),
            Kind.Priority => (await db.WorkItemPriorities.Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.Name }).ToListAsync(ct))
                .Select(x => (x.Id, x.Name)).ToList(),
            Kind.User => (await db.Users.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.Username }).ToListAsync(ct))
                .Select(x => (x.Id, x.Username)).ToList(),
            _ => (await db.WorkItems.Where(w => ids.Contains(w.Id)).Select(w => new { w.Id, w.Number, w.Title }).ToListAsync(ct))
                .Select(x => (x.Id, $"#{x.Number} {x.Title}")).ToList(),
        };
}
