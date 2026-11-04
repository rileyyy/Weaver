namespace Weaver.Domain;

/// <summary>
/// One field a write changed, with its value before and after. <see cref="Field"/> is a
/// <see cref="WorkItemField"/> name, or <see cref="WorkItemField.CustomField"/>'s for a custom
/// field. Values keep the field's own type (a list for tags, a JSON value for a custom field).
/// </summary>
public sealed record FieldChange(string Field, object? OldValue, object? NewValue);
