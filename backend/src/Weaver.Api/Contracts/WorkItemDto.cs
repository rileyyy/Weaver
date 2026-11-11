using System.Text.Json;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// <see cref="Fields"/> holds the values of the item's Type's custom fields by field id (see
/// <see cref="CustomFieldDto"/>); a field with no value is left out. <see cref="TrashedAtUtc"/> is
/// set only for an item in the Trash, which a read returns only when it asked for deleted items.
/// <see cref="WhenResolved"/> and <see cref="BlockedBy"/> depend on the item's live blockers, and
/// <see cref="WhenEditedSinceResolved"/> on its history (ticket 49), so build it through
/// <see cref="WorkItemDtoMapper"/>, which looks them up; <see cref="BlockedBy"/>
/// is empty, never null, when nothing blocks the item. <see cref="CompletionDate"/> is the day
/// (in the server's time zone) the item entered a Done status, and null while it isn't Done.
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
    bool WhenEditedSinceResolved,
    IReadOnlyList<WorkItemBlockerDto> BlockedBy,
    DateOnly? CompletionDate,
    Guid? RecurrenceSourceId,
    DateOnly? RecurrenceDate,
    DateTimeOffset? TrashedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    uint Version)
{
    public static WorkItemDto FromEntity(
        WorkItem item,
        IReadOnlyList<WorkItemBlocker> blockers,
        WorkItemNames names,
        bool whenEditedSinceResolved = false) =>
        FromEntity(item, blockers, names, names.WorkItem(item.ParentId), whenEditedSinceResolved);

    private static WorkItemDto FromEntity(
        WorkItem item,
        IReadOnlyList<WorkItemBlocker> blockers,
        WorkItemNames names,
        WorkItemReference? parent,
        bool whenEditedSinceResolved) => new(
        item.Id,
        item.Number,
        item.SpaceId,
        names.SpaceName(item.SpaceId),
        item.ParentId,
        parent?.Number,
        parent?.Title,
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
        item.IsWhenResolved(hasLiveBlocker: blockers.Count > 0),
        whenEditedSinceResolved,
        // Not a collection expression: ToArray hands back the shared empty array when nothing
        // blocks the item, so two DTOs of the same unblocked item stay equal as records.
        blockers.Select(WorkItemBlockerDto.FromBlocker).ToArray(),
        item.CompletionDate,
        item.RecurrenceSourceId,
        item.RecurrenceDate,
        item.TrashedAtUtc,
        item.CreatedAtUtc,
        item.UpdatedAtUtc,
        item.Version);
}
