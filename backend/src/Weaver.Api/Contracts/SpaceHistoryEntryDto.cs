using System.Text.Json;
using System.Text.Json.Serialization;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// One change to a Space. <see cref="Field"/> is a <see cref="SpaceHistoryField"/> name: created
/// (new value: the name), name (old and new name) or doneHidingDays (old and new number of days).
/// A null account is Weaver itself.
/// </summary>
/// <remarks>
/// The values are written even when null, including over MCP, for the reason
/// <see cref="WorkItemHistoryEntryDto"/> gives.
/// </remarks>
public record SpaceHistoryEntryDto(
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JsonElement? OldValue,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JsonElement? NewValue)
{
    public static SpaceHistoryEntryDto FromView(SpaceHistoryView view) => new(
        view.Id,
        view.SpaceId,
        view.ChangedAtUtc,
        view.AccountId,
        view.AccountName,
        view.AccountKind,
        view.ApiKeyId,
        view.ApiKeyLabel,
        view.ActorLabel,
        view.Field,
        view.OldValue,
        view.NewValue);
}
