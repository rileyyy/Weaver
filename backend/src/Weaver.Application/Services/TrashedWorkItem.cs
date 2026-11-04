using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// One delete in the Trash: the item it was made on, when, and by whom. Its descendants went
/// with it and come back with it, so they aren't listed on their own.
/// </summary>
/// <param name="ItemCount">The item plus every descendant trashed with it.</param>
/// <param name="DeletedByName">The account that deleted it; null if history doesn't say.</param>
/// <param name="DeletedByActorLabel">What an agent said it was acting as, if it said.</param>
public sealed record TrashedWorkItem(
    WorkItemSummary Item,
    DateTimeOffset TrashedAtUtc,
    int ItemCount,
    string? DeletedByName,
    UserKind? DeletedByKind,
    string? DeletedByActorLabel);
