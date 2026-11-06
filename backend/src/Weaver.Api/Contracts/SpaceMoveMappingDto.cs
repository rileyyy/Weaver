using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// One status, Type or priority of the moving items and its default in the target Space; a
/// null target means a Type or priority that would be cleared.
/// </summary>
public record SpaceMoveMappingDto(Guid SourceId, string SourceName, Guid? TargetId, string? TargetName, int ItemCount)
{
    public static SpaceMoveMappingDto FromModel(SpaceMoveMapping mapping) => new(
        mapping.SourceId, mapping.SourceName, mapping.TargetId, mapping.TargetName, mapping.ItemCount);
}
