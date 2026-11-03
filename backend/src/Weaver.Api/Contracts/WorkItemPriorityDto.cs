using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record WorkItemPriorityDto(Guid Id, Guid SpaceId, string Name, int Order)
{
    public static WorkItemPriorityDto FromEntity(WorkItemPriority priority) =>
        new(priority.Id, priority.SpaceId, priority.Name, priority.Order);
}
