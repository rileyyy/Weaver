using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>One side of a <see cref="DependencyConflictDto"/>: the item, its Space by name, and its dates.</summary>
public record DependencyConflictItemDto(
    Guid Id,
    int Number,
    string Title,
    Guid SpaceId,
    string SpaceName,
    DateOnly? StartDate,
    DateOnly? EndDate)
{
    public static DependencyConflictItemDto FromItem(DependencyConflictItem item) =>
        new(item.Id, item.Number, item.Title, item.SpaceId, item.SpaceName, item.StartDate, item.EndDate);
}
