using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record WorkItemLayerDto(Guid Id, string Name, int Order)
{
    public static WorkItemLayerDto FromEntity(WorkItemLayer layer) => new(layer.Id, layer.Name, layer.Order);
}
