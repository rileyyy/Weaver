using System.Text.Json;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// One change to a work item. <see cref="Field"/> is a work item field name (as in its JSON)
/// or one of created, deleted, comment, link and recurrence. The values are JSON in the shape
/// that field has in the API; <see cref="OldName"/>/<see cref="NewName"/> name an id value
/// (a status, user or other item) while it still exists. A custom field's changes go by
/// <c>fields.&lt;fieldId&gt;</c>, with <see cref="FieldName"/> its current name (null for other
/// fields, or a field since removed). A null account is Weaver itself.
/// </summary>
public record WorkItemHistoryEntryDto(
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
    string? NewName,
    string? FieldName)
{
    public static WorkItemHistoryEntryDto FromView(WorkItemHistoryView view) => new(
        view.Id,
        view.WorkItemId,
        view.ChangedAtUtc,
        view.AccountId,
        view.AccountName,
        view.AccountKind,
        view.ApiKeyId,
        view.ApiKeyLabel,
        view.ActorLabel,
        view.Field,
        view.OldValue,
        view.NewValue,
        view.OldName,
        view.NewName,
        view.FieldName);
}
