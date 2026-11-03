using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record StatusDto(Guid Id, Guid SpaceId, string Name, int Order, StatusCategory Category, string Color)
{
    public static StatusDto FromEntity(Status status) =>
        new(status.Id, status.SpaceId, status.Name, status.Order, status.Category, status.Color);
}
