namespace Weaver.Application.Services;

/// <summary>A live blocks link with both items' details, projected by <see cref="BlockedWorkItems.LiveBlocksLinkRows"/>.</summary>
internal sealed class BlocksLinkRow
{
    public required BlocksLinkSide Blocked { get; init; }

    public required BlocksLinkSide Blocker { get; init; }

    public bool Conflicts { get; init; }

    public DependencyConflict ToConflict() => new(Blocked.ToConflictItem(), Blocker.ToConflictItem());

    public WorkItemBlocker ToBlocker() => new(Blocker.Id, Blocker.Number, Blocker.Title, Conflicts);
}
