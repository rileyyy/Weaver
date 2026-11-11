using System.Text.Json;
using System.Text.Json.Serialization;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// One change to a work item. <see cref="Field"/> is a work item field name (as in its JSON)
/// or one of the <see cref="WorkItemHistoryField"/> names (created, deleted, restored, purged, comment,
/// link, urlLink, attachment and recurrence). The values are JSON in the shape
/// that field has in the API; <see cref="OldName"/>/<see cref="NewName"/> name an id value
/// (a status, user or other item) while it still exists. A custom field's changes go by
/// <c>fields.&lt;fieldId&gt;</c>, with <see cref="FieldName"/> its current name (null for other
/// fields, or a field since removed). A null account is Weaver itself. <see cref="RunId"/> groups
/// the changes one agent run made (ADR-0010).
/// </summary>
/// <remarks>
/// The values are written even when null, including over MCP, which otherwise leaves nulls out:
/// a null there says the field was empty before or was cleared, which is what the entry records,
/// and a reader shouldn't have to infer it from a missing property.
/// </remarks>
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
    string? RunId,
    string Field,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JsonElement? OldValue,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JsonElement? NewValue,
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
        view.RunId,
        view.Field,
        view.OldValue,
        view.NewValue,
        view.OldName,
        view.NewName,
        view.FieldName);
}
