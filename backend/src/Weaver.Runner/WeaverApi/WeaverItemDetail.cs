namespace Weaver.Runner.WeaverApi;

/// <summary>
/// The parts of Weaver's <c>WorkItemDto</c> a mention's answer needs: enough to describe the item
/// to the model, add sub-items in its Space, and set dates only if it has none.
/// </summary>
public sealed record WeaverItemDetail(
    Guid Id,
    int Number,
    string Title,
    string? Description,
    Guid SpaceId,
    string? SpaceName,
    string? StatusName,
    string? When,
    DateOnly? StartDate,
    DateOnly? EndDate,
    uint Version)
{
    public bool HasDates => StartDate is not null || EndDate is not null;
}
