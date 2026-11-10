namespace Weaver.Domain;

/// <summary>The sum of one roll-up field over an item's descendants, not counting the item's own value.</summary>
public sealed record RollUpTotal(CustomFieldDefinition Field, decimal Total);
