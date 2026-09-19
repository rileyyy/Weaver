using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record StatusDto(Guid Id, string Name, int Order, StatusCategory Category, string Color)
{
    public static StatusDto FromEntity(Status status) =>
        new(status.Id, status.Name, status.Order, status.Category, status.Color);
}
