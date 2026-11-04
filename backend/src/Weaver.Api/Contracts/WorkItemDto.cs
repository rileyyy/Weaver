using System.Text.Json;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// <see cref="Fields"/> holds the values of the item's Type's custom fields by field id (see
/// <see cref="CustomFieldDto"/>); a field with no value is left out. <see cref="TrashedAtUtc"/> is
/// set only for an item in the Trash, which a read returns only when it asked for deleted items.
/// </summary>
public record WorkItemDto(
    Guid Id,
    int Number,
    Guid SpaceId,
    Guid? ParentId,
    string Title,
    string? Description,
    Guid StatusId,
    Guid? TypeId,
    Guid? PriorityId,
    Guid? AssignedToUserId,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<Guid, JsonElement> Fields,
    double Rank,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? When,
    bool WhenResolved,
    Guid? RecurrenceSourceId,
    DateOnly? RecurrenceDate,
    DateTimeOffset? TrashedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    uint Version)
{
    public static WorkItemDto FromEntity(WorkItem item) => new(
        item.Id,
        item.Number,
        item.SpaceId,
        item.ParentId,
        item.Title,
        item.Description,
        item.StatusId,
        item.TypeId,
        item.PriorityId,
        item.AssignedToUserId,
        item.Tags,
        item.CustomFields,
        item.Rank,
        item.StartDate,
        item.EndDate,
        item.When,
        item.IsWhenResolved,
        item.RecurrenceSourceId,
        item.RecurrenceDate,
        item.TrashedAtUtc,
        item.CreatedAtUtc,
        item.UpdatedAtUtc,
        item.Version);
}
