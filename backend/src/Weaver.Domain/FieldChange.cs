namespace Weaver.Domain;

/// <summary>
/// One field a write changed, with its value before and after. <see cref="Field"/> is a
/// <see cref="WorkItemField"/> name. Values keep the field's own type (a list for tags).
/// </summary>
public sealed record FieldChange(string Field, object? OldValue, object? NewValue);
