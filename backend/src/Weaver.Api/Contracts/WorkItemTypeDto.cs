using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record WorkItemTypeDto(Guid Id, Guid SpaceId, string Name, int Order, string Color)
{
    public static WorkItemTypeDto FromEntity(WorkItemType type) =>
        new(type.Id, type.SpaceId, type.Name, type.Order, type.Color);
}
