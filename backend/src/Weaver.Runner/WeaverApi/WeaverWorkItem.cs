namespace Weaver.Runner.WeaverApi;

/// <summary>
/// The parts of Weaver's <c>WorkItemDto</c> the runner reads. <see cref="WhenEditedSinceResolved"/>
/// is Weaver's mark for an item whose When a human edited after its dates were set (ticket 49).
/// </summary>
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
    uint Version,
    bool WhenEditedSinceResolved = false)
{
    public bool HasDates => StartDate is not null || EndDate is not null;
}
