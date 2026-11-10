namespace Weaver.Application.Services;

/// <summary>One item of a <see cref="BlocksLinkRow"/>, with init setters for the same reason the row has them.</summary>
internal sealed class BlocksLinkSide
{
    public Guid Id { get; init; }

    public int Number { get; init; }

    public string Title { get; init; } = "";

    public Guid SpaceId { get; init; }

    public string SpaceName { get; init; } = "";

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public DependencyConflictItem ToConflictItem() => new(Id, Number, Title, SpaceId, SpaceName, StartDate, EndDate);
}
