using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// One delete in the Trash: the item it was made on, when, by whom, and how many items (it and
/// its descendants) restoring it brings back.
/// </summary>
public record TrashedWorkItemDto(
    WorkItemSummaryDto Item,
    DateTimeOffset TrashedAtUtc,
    int ItemCount,
    string? DeletedByName,
    UserKind? DeletedByKind,
    string? DeletedByActorLabel)
{
    public static TrashedWorkItemDto FromModel(TrashedWorkItem trashed) => new(
        WorkItemSummaryDto.FromSummary(trashed.Item),
        trashed.TrashedAtUtc,
        trashed.ItemCount,
        trashed.DeletedByName,
        trashed.DeletedByKind,
        trashed.DeletedByActorLabel);
}
