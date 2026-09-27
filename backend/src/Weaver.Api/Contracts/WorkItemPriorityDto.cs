using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record WorkItemPriorityDto(Guid Id, string Name, int Order)
{
    public static WorkItemPriorityDto FromEntity(WorkItemPriority priority) =>
        new(priority.Id, priority.Name, priority.Order);
}
