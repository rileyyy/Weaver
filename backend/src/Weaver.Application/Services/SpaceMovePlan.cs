namespace Weaver.Application.Services;

/// <summary>
/// What moving a work item into another Space would do, before it's confirmed: how many items
/// go (it and its live descendants), the default for each value they use, and how many custom
/// field values would be cleared, since Types are per Space (ADR-0001, ADR-0002).
/// </summary>
public sealed record SpaceMovePlan(
    Guid WorkItemId,
    Guid TargetSpaceId,
    int ItemCount,
    int ClearedFieldValueCount,
    IReadOnlyList<SpaceMoveMapping> Statuses,
    IReadOnlyList<SpaceMoveMapping> Types,
    IReadOnlyList<SpaceMoveMapping> Priorities);
