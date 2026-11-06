using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// A field a Type defines. A work item's value for it is in the item's <c>fields</c>, under
/// <see cref="Id"/>; <see cref="Options"/> is empty unless it's a select field. When
/// <see cref="RollsUp"/>, a parent's detail also shows the field's sum over its descendants.
/// </summary>
public record CustomFieldDto(
    Guid Id,
    Guid TypeId,
    string Name,
    CustomFieldKind Kind,
    int Order,
    IReadOnlyList<CustomFieldOptionDto> Options,
    bool RollsUp)
{
    public static CustomFieldDto FromEntity(CustomFieldDefinition field) => new(
        field.Id,
        field.TypeId,
        field.Name,
        field.Kind,
        field.Order,
        field.Options.Select(CustomFieldOptionDto.FromOption).ToList(),
        field.RollsUp);
}
