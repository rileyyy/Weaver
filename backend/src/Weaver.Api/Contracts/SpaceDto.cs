using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record SpaceDto(Guid Id, string Name)
{
    public static SpaceDto FromEntity(Space space) => new(space.Id, space.Name);
}
