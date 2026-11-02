using System.Text.Json;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// One history entry as readers see it: who (account, its kind, the key and actor label),
/// when, and the field's old and new values as JSON. <see cref="OldName"/> and
/// <see cref="NewName"/> name what an id value refers to (a status, a user, another item),
/// when that still exists.
/// </summary>
public sealed record WorkItemHistoryView(
    long Id,
    Guid WorkItemId,
    DateTimeOffset ChangedAtUtc,
    Guid? AccountId,
    string? AccountName,
    UserKind? AccountKind,
    Guid? ApiKeyId,
    string? ApiKeyLabel,
    string? ActorLabel,
    string Field,
    JsonElement? OldValue,
    JsonElement? NewValue,
    string? OldName,
    string? NewName);
