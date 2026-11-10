namespace Weaver.Application.Services;

/// <summary>
/// A work item in brief, with names instead of ids, so a reader of a list needs no lookups.
/// Only <see cref="Id"/> and <see cref="Title"/> are always known: the rest is null for an item
/// that has been purged, since only its history remains. <see cref="TrashedAtUtc"/> is set for an
/// item in the Trash, which a read only returns when it asked for deleted items.
/// <see cref="ParentId"/> is null for a top-level item, and for a purged one, so a reader can
/// rebuild the tree from a flat list of summaries. <see cref="When"/> is the item's When phrase,
/// kept after it's resolved.
/// </summary>
public sealed record WorkItemSummary(
    Guid Id,
    int? Number,
    string Title,
    Guid? SpaceId,
    string? SpaceName,
    Guid? ParentId,
    string? StatusName,
    string? TypeName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? AssigneeName,
    int? ChildCount,
    DateTimeOffset? TrashedAtUtc = null,
    string? When = null);
