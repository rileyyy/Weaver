using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// A dependency conflict (ADR-0009): <see cref="Blocked"/> starts before <see cref="Blocker"/>,
/// which blocks it, ends.
/// </summary>
public record DependencyConflictDto(DependencyConflictItemDto Blocked, DependencyConflictItemDto Blocker)
{
    public static DependencyConflictDto FromConflict(DependencyConflict conflict) =>
        new(DependencyConflictItemDto.FromItem(conflict.Blocked), DependencyConflictItemDto.FromItem(conflict.Blocker));
}
