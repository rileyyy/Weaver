namespace Weaver.Runner.WeaverApi;

/// <summary>The parts of Weaver's <c>WorkItemDto</c> the runner reads.</summary>
public sealed record WeaverWorkItem(
    Guid Id,
    int Number,
    string Title,
    string? When,
    Guid SpaceId,
    string? SpaceName,
    int? ParentNumber,
    string? ParentTitle,
    DateOnly? StartDate,
    DateOnly? EndDate,
    uint Version)
{
    public bool HasDates => StartDate is not null || EndDate is not null;
}
