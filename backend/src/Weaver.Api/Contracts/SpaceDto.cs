using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// <see cref="DoneHidingDays"/> is how many days a Done item stays on the Board after its
/// completion date.
/// </summary>
public record SpaceDto(Guid Id, string Name, int DoneHidingDays)
{
    public static SpaceDto FromEntity(Space space) => new(space.Id, space.Name, space.DoneHidingDays);
}
