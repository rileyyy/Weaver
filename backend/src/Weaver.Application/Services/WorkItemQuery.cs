using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Which work items to list (ADR-0006). Every input left out applies no filter, and the inputs
/// given AND together; a list input matches any of its values unless it says otherwise. Pass the
/// same values with each page's <see cref="Cursor"/>: the cursor is a position in these results.
/// </summary>
/// <param name="SpaceIds">Only items in these Spaces.</param>
/// <param name="Text">
/// Words that must all appear, as words or the start of one, in the title, description or When.
/// Text that is just <c>#123</c> looks up that <see cref="Number"/> instead.
/// </param>
/// <param name="Number">Only the item with this Number.</param>
/// <param name="StatusCategories">Only items whose status is in one of these categories.</param>
/// <param name="AssigneeId">Only items assigned to this user.</param>
/// <param name="Tags">Matched ignoring case and combined by <see cref="TagMatch"/>. Tags are global across Spaces.</param>
/// <param name="TypeIds">Only items of one of these Types.</param>
/// <param name="From">Start of the date window, inclusive. See <see cref="IWorkItemQueryService"/> for the rule.</param>
/// <param name="To">End of the date window, inclusive.</param>
/// <param name="BacklogOnly">Only Backlog items: not Done and with no dates (ADR-0003).</param>
/// <param name="UnresolvedWhenOnly">
/// Only items whose When is unresolved: they have one, but no dates and no live item blocking
/// them (ADR-0003, <see cref="WorkItem.IsWhenResolved"/>).
/// </param>
/// <param name="ScheduledOrInProgressOnly">
/// Only items in a Doing-category status or with dates overlapping the window (with no window,
/// any dated item), plus the undated ancestors of those. This is Overview's rule (ADR-0006): the
/// window narrows only the dated items, never the Doing ones.
/// </param>
/// <param name="ApplyDoneHiding">
/// Leave out Done items completed longer ago than their Space's done-hiding period
/// (<see cref="Space.DoneHidingDays"/>), counted back from the server's today. An item completed
/// exactly that many days ago, or a Done item with no completion date, is still listed.
/// </param>
/// <param name="UpdatedSince">Only items last updated at or after this moment.</param>
/// <param name="IncludeDeleted">
/// Also list items in the Trash, which every other read leaves out (ADR-0007). They're marked
/// by their trash time.
/// </param>
public sealed record WorkItemQuery(
    IReadOnlyList<Guid>? SpaceIds = null,
    string? Text = null,
    int? Number = null,
    IReadOnlyList<StatusCategory>? StatusCategories = null,
    Guid? AssigneeId = null,
    IReadOnlyList<string>? Tags = null,
    TagMatch TagMatch = TagMatch.Any,
    IReadOnlyList<Guid>? TypeIds = null,
    DateOnly? From = null,
    DateOnly? To = null,
    bool BacklogOnly = false,
    bool UnresolvedWhenOnly = false,
    bool ScheduledOrInProgressOnly = false,
    bool ApplyDoneHiding = false,
    DateTimeOffset? UpdatedSince = null,
    bool IncludeDeleted = false,
    string? Cursor = null,
    int Limit = WorkItemQuery.DefaultLimit)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 1000;

    public bool HasDateWindow => From is not null || To is not null;

    /// <summary>Whether matching needs the dated-item rule, which brings in undated ancestors.</summary>
    public bool JudgesByDates => HasDateWindow || ScheduledOrInProgressOnly;
}
