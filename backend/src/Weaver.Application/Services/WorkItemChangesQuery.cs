namespace Weaver.Application.Services;

/// <summary>
/// Which changes to list. <see cref="Since"/> is inclusive. Pass the same values with each
/// page's <see cref="Cursor"/>: the cursor is a position in this query's results.
/// </summary>
/// <param name="SpaceId">Only items in this Space; a deleted item counts as in the Space it was last in.</param>
/// <param name="ExcludeOwnChanges">Leave out the caller's own entries, so an agent sees what others did.</param>
public sealed record WorkItemChangesQuery(
    DateTimeOffset Since,
    Guid? SpaceId = null,
    bool ExcludeOwnChanges = false,
    string? Cursor = null,
    int Limit = WorkItemChangesQuery.DefaultLimit)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
}
