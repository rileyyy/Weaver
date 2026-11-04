using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>A Type with its custom fields, so an agent learns both in one call.</summary>
public record WorkItemTypeWithFieldsDto(
    Guid Id,
    Guid SpaceId,
    string Name,
    int Order,
    string Color,
    IReadOnlyList<CustomFieldDto> Fields)
{
    public static WorkItemTypeWithFieldsDto FromEntity(WorkItemType type, IEnumerable<CustomFieldDefinition> fields) =>
        new(type.Id, type.SpaceId, type.Name, type.Order, type.Color, fields.Select(CustomFieldDto.FromEntity).ToList());
}
