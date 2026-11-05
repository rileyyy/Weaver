using System.Text.Json;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// <see cref="Fields"/> holds the values of the item's Type's custom fields by field id (see
/// <see cref="CustomFieldDto"/>); a field with no value is left out. <see cref="TrashedAtUtc"/> is
/// set only for an item in the Trash, which a read returns only when it asked for deleted items.
/// <see cref="WhenResolved"/> depends on the item's blockers, so build it through
/// <see cref="WorkItemDtoMapper"/>, which looks them up.
/// Each id comes with its name (the parent with its Number and title) so a reader needn't look
/// them up; a name is null when its id is, or when the row it names is gone.
/// </summary>
public record WorkItemDto(
    Guid Id,
    int Number,
    Guid SpaceId,
    string? SpaceName,
    Guid? ParentId,
    int? ParentNumber,
    string? ParentTitle,
    string Title,
    string? Description,
    Guid StatusId,
    string? StatusName,
    Guid? TypeId,
    string? TypeName,
    Guid? PriorityId,
    string? PriorityName,
    Guid? AssignedToUserId,
    string? AssignedToUsername,
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
    public static WorkItemDto FromEntity(WorkItem item, bool hasLiveBlocker, WorkItemNames names) => new(
        item.Id,
        item.Number,
        item.SpaceId,
        names.SpaceName(item.SpaceId),
        item.ParentId,
        names.WorkItem(item.ParentId)?.Number,
        names.WorkItem(item.ParentId)?.Title,
        item.Title,
        item.Description,
        item.StatusId,
        names.StatusName(item.StatusId),
        item.TypeId,
        names.TypeName(item.TypeId),
        item.PriorityId,
        names.PriorityName(item.PriorityId),
        item.AssignedToUserId,
        names.Username(item.AssignedToUserId),
        item.Tags,
        item.CustomFields,
        item.Rank,
        item.StartDate,
        item.EndDate,
        item.When,
        item.IsWhenResolved(hasLiveBlocker),
        item.RecurrenceSourceId,
        item.RecurrenceDate,
        item.TrashedAtUtc,
        item.CreatedAtUtc,
        item.UpdatedAtUtc,
        item.Version);
}
