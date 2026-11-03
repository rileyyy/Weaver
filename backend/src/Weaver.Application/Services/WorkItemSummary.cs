namespace Weaver.Application.Services;

/// <summary>
/// A work item in brief, with names instead of ids, so a reader of a list needs no lookups.
/// Only <see cref="Id"/> and <see cref="Title"/> are always known: the rest is null for an item
/// that has been deleted, since only its history remains.
/// </summary>
public sealed record WorkItemSummary(
    Guid Id,
    int? Number,
    string Title,
    Guid? SpaceId,
    string? SpaceName,
    string? StatusName,
    string? TypeName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? AssigneeName,
    int? ChildCount);
