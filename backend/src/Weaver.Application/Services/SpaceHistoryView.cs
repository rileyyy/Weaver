using System.Text.Json;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// One Space history entry as readers see it: who (account, its kind, the key and actor label),
/// when, and the old and new values as JSON. Unlike <see cref="WorkItemHistoryView"/> there are no
/// names to look up: no <see cref="SpaceHistoryField"/> holds an id.
/// </summary>
public sealed record SpaceHistoryView(
    long Id,
    Guid SpaceId,
    DateTimeOffset ChangedAtUtc,
    Guid? AccountId,
    string? AccountName,
    UserKind? AccountKind,
    Guid? ApiKeyId,
    string? ApiKeyLabel,
    string? ActorLabel,
    string Field,
    JsonElement? OldValue,
    JsonElement? NewValue);
