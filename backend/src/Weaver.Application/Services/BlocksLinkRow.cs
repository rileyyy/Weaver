namespace Weaver.Application.Services;

/// <summary>A live blocks link with both items' details, projected by <see cref="BlockedWorkItems.LiveBlocksLinkRows"/>.</summary>
internal sealed class BlocksLinkRow
{
    public Guid BlockedId { get; init; }

    public int BlockedNumber { get; init; }

    public string BlockedTitle { get; init; } = "";

    public Guid BlockedSpaceId { get; init; }

    public string BlockedSpaceName { get; init; } = "";

    public DateOnly? BlockedStartDate { get; init; }

    public DateOnly? BlockedEndDate { get; init; }

    public Guid BlockerId { get; init; }

    public int BlockerNumber { get; init; }

    public string BlockerTitle { get; init; } = "";

    public Guid BlockerSpaceId { get; init; }

    public string BlockerSpaceName { get; init; } = "";

    public DateOnly? BlockerStartDate { get; init; }

    public DateOnly? BlockerEndDate { get; init; }

    public bool Conflicts { get; init; }

    public DependencyConflict ToConflict() => new(
        new DependencyConflictItem(BlockedId, BlockedNumber, BlockedTitle, BlockedSpaceId, BlockedSpaceName, BlockedStartDate, BlockedEndDate),
        new DependencyConflictItem(BlockerId, BlockerNumber, BlockerTitle, BlockerSpaceId, BlockerSpaceName, BlockerStartDate, BlockerEndDate));

    public WorkItemBlocker ToBlocker() => new(BlockerId, BlockerNumber, BlockerTitle, BlockerSpaceId, BlockerEndDate, Conflicts);
}
