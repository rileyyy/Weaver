using System.Text.Json;
using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// Custom field values checked against one Type's field definitions and normalized, ready for
/// <see cref="WorkItem.SetCustomFields"/>. Only <see cref="Validate"/> makes one, so an item can
/// never store a value its field hasn't accepted. A field left out is unchanged; a <c>null</c>
/// value clears it.
/// </summary>
public sealed class CustomFieldPatch
{
    private CustomFieldPatch(Guid typeId, IReadOnlyList<(CustomFieldDefinition Field, JsonElement? Value)> values)
    {
        TypeId = typeId;
        Values = values;
    }

    /// <summary>The Type the values were validated for; the item must still have it.</summary>
    public Guid TypeId { get; }

    public IReadOnlyList<(CustomFieldDefinition Field, JsonElement? Value)> Values { get; }

    /// <summary>
    /// The users the values name, which must exist: that needs the database, so the caller
    /// checks it.
    /// </summary>
    public IReadOnlyList<Guid> UserIds =>
        Values.Where(v => v.Field.Kind == CustomFieldKind.User && v.Value is not null)
            .Select(v => v.Value!.Value.GetGuid())
            .Distinct()
            .ToList();

    /// <param name="typeId">The item's Type; an item without one has no fields.</param>
    /// <param name="typeFields">Every field definition of that Type.</param>
    /// <exception cref="DomainValidationException">A field isn't one of the Type's, or a value doesn't fit its field.</exception>
    public static CustomFieldPatch Validate(
        Guid? typeId,
        IReadOnlyCollection<CustomFieldDefinition> typeFields,
        IReadOnlyDictionary<Guid, JsonElement> values)
    {
        if (typeId is not { } type)
        {
            if (values.Count > 0)
            {
                throw new DomainValidationException("A work item without a Type has no custom fields.");
            }

            return new CustomFieldPatch(Guid.Empty, []);
        }

        var fields = typeFields.Where(f => f.TypeId == type).ToDictionary(f => f.Id);
        var normalized = values
            .Select(v => fields.TryGetValue(v.Key, out var field)
                ? (field, field.Normalize(v.Value))
                : throw new DomainValidationException($"Field {v.Key} isn't one of the item's Type's fields."))
            .ToList();
        return new CustomFieldPatch(type, normalized);
    }
}
