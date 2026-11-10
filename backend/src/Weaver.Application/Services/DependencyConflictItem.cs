namespace Weaver.Application.Services;

/// <summary>One side of a <see cref="DependencyConflict"/>, named so a reader needs no lookups.</summary>
public sealed record DependencyConflictItem(
    Guid Id,
    int Number,
    string Title,
    Guid SpaceId,
    string SpaceName,
    DateOnly? StartDate,
    DateOnly? EndDate);
