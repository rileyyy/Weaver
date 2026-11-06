using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// What moving a work item to another Space would do: how many items go, how many custom field
/// values are cleared, and each value's default in the target Space.
/// </summary>
public record SpaceMovePlanDto(
    Guid WorkItemId,
    Guid TargetSpaceId,
    int ItemCount,
    int ClearedFieldValueCount,
    IReadOnlyList<SpaceMoveMappingDto> Statuses,
    IReadOnlyList<SpaceMoveMappingDto> Types,
    IReadOnlyList<SpaceMoveMappingDto> Priorities)
{
    public static SpaceMovePlanDto FromModel(SpaceMovePlan plan) => new(
        plan.WorkItemId,
        plan.TargetSpaceId,
        plan.ItemCount,
        plan.ClearedFieldValueCount,
        plan.Statuses.Select(SpaceMoveMappingDto.FromModel).ToList(),
        plan.Types.Select(SpaceMoveMappingDto.FromModel).ToList(),
        plan.Priorities.Select(SpaceMoveMappingDto.FromModel).ToList());
}
