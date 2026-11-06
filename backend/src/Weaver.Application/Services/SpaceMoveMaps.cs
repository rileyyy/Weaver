namespace Weaver.Application.Services;

/// <summary>
/// The caller's choices for a move between Spaces: which of the target Space's statuses, Types
/// and priorities each of the item's Space's values becomes, keyed by the item's Space's value.
/// A Type or priority mapped to null is cleared. A value left out takes its default (see
/// <see cref="IWorkItemSpaceMoveService"/>).
/// </summary>
public sealed record SpaceMoveMaps(
    IReadOnlyDictionary<Guid, Guid> Statuses,
    IReadOnlyDictionary<Guid, Guid?> Types,
    IReadOnlyDictionary<Guid, Guid?> Priorities)
{
    /// <summary>Every value takes its default.</summary>
    public static SpaceMoveMaps None { get; } = new(
        new Dictionary<Guid, Guid>(),
        new Dictionary<Guid, Guid?>(),
        new Dictionary<Guid, Guid?>());

    /// <summary>Maps as REST and MCP receive them, where a map left out means none.</summary>
    public static SpaceMoveMaps From(
        IReadOnlyDictionary<Guid, Guid>? statuses,
        IReadOnlyDictionary<Guid, Guid?>? types,
        IReadOnlyDictionary<Guid, Guid?>? priorities) =>
        new(statuses ?? None.Statuses, types ?? None.Types, priorities ?? None.Priorities);
}
