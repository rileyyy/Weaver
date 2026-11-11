using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemQueryService(
    IWeaverDbContext db,
    IWorkItemHierarchy hierarchy,
    IWorkItemSearch search,
    IServerCalendar calendar) : IWorkItemQueryService
{
    private readonly IWeaverDbContext _db = db;
    private readonly IWorkItemHierarchy _hierarchy = hierarchy;
    private readonly IWorkItemSearch _search = search;
    private readonly IServerCalendar _calendar = calendar;

    public async Task<WorkItemQueryPage<WorkItem>> QueryAsync(WorkItemQuery query, CancellationToken ct = default)
    {
        var page = await PageAsync(query, ct);
        var items = await page.Items.AsNoTracking().ToListAsync(ct);
        return new WorkItemQueryPage<WorkItem>(items, page.NextCursor);
    }

    public async Task<WorkItemQueryPage<WorkItemSummary>> QuerySummariesAsync(WorkItemQuery query, CancellationToken ct = default)
    {
        var page = await PageAsync(query, ct);
        var items = await page.Items.AsNoTracking().Summarize(_db).ToListAsync(ct);
        return new WorkItemQueryPage<WorkItemSummary>(items, page.NextCursor);
    }

    /// <summary>
    /// The page's items, still as a query so each caller can shape them. Reads one item past the
    /// limit to learn whether there's a next page, then pins the page to the Numbers it found.
    /// </summary>
    private async Task<(IQueryable<WorkItem> Items, string? NextCursor)> PageAsync(WorkItemQuery query, CancellationToken ct)
    {
        if (query.Limit is < 1 or > WorkItemQuery.MaxLimit)
        {
            throw new DomainValidationException($"Limit must be between 1 and {WorkItemQuery.MaxLimit}.");
        }

        var afterNumber = WorkItemQueryCursor.Parse(query.Cursor).AfterNumber;
        var numbers = await (await MatchingAsync(query, ct))
            .Where(w => w.Number > afterNumber)
            .OrderBy(w => w.Number)
            .Select(w => w.Number)
            .Take(query.Limit + 1)
            .ToListAsync(ct);
        var onPage = numbers.Take(query.Limit).ToList();
        var nextCursor = numbers.Count > query.Limit ? new WorkItemQueryCursor(onPage[^1]).Encode() : null;

        return (Candidates(query).Where(w => onPage.Contains(w.Number)).OrderBy(w => w.Number), nextCursor);
    }

    private async Task<IQueryable<WorkItem>> MatchingAsync(WorkItemQuery query, CancellationToken ct)
    {
        var items = Candidates(query);
        items = InSpaces(items, query.SpaceIds);
        items = MatchingText(items, query.Text);
        items = Numbered(items, query.Number);
        items = InStatusCategories(items, query.StatusCategories);
        items = AssignedTo(items, query.AssigneeId);
        items = Tagged(items, query.Tags, query.TagMatch);
        items = OfTypes(items, query.TypeIds);
        items = UpdatedSince(items, query.UpdatedSince);
        items = InBacklog(items, query.BacklogOnly);
        items = WithUnresolvedWhen(items, query);
        items = await WithoutHiddenDoneAsync(items, query.ApplyDoneHiding, ct);
        return query.JudgesByDates ? await ByDatesAsync(items, query, ct) : items;
    }

    /// <summary>
    /// Each item is judged by its own Space's period, since one query can span Spaces. Today is
    /// only read when the filter is asked for, so other queries cost no settings lookup.
    /// </summary>
    private async Task<IQueryable<WorkItem>> WithoutHiddenDoneAsync(
        IQueryable<WorkItem> items,
        bool applyDoneHiding,
        CancellationToken ct)
    {
        if (!applyDoneHiding)
        {
            return items;
        }

        var today = await _calendar.TodayAsync(ct);
        return items.Where(w =>
            w.Status!.Category != StatusCategory.Done
            || w.CompletionDate == null
            || w.CompletionDate.Value.AddDays(w.Space!.DoneHidingDays) >= today);
    }

    /// <summary>Include-deleted is the one way to see the Trash through the query (ADR-0007).</summary>
    private IQueryable<WorkItem> Candidates(WorkItemQuery query) =>
        query.IncludeDeleted ? _db.WorkItems.IgnoreQueryFilters() : _db.WorkItems;

    /// <summary>
    /// ADR-0003's rule: an item with dates matches when they overlap the window; an item with
    /// none matches only through a descendant that matches the whole query, so a Board or Roadmap
    /// can still draw the undated parent a listed item sits under, and never one with nothing
    /// listed beneath it. A dated parent is judged by its own dates alone. Overview's rule
    /// (<see cref="WorkItemQuery.ScheduledOrInProgressOnly"/>) also lists every Doing item,
    /// whatever its dates, and brings in its undated ancestors the same way.
    /// </summary>
    /// <param name="items">Already narrowed by every other input.</param>
    private async Task<IQueryable<WorkItem>> ByDatesAsync(IQueryable<WorkItem> items, WorkItemQuery query, CancellationToken ct)
    {
        if (query is { From: { } from, To: { } to } && from > to)
        {
            throw new DomainValidationException("The date window can't end before it starts.");
        }

        var datedInWindow = DatedInWindow(items, query.From, query.To);
        var listed = query.ScheduledOrInProgressOnly
            ? items.Where(w => w.Status!.Category == StatusCategory.Doing || datedInWindow.Any(d => d.Id == w.Id))
            : datedInWindow;
        var parentsOfListed = await listed
            .Where(w => w.ParentId != null)
            .Select(w => w.ParentId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var ancestorsOfListed = await _hierarchy.GetSelfAndAncestorIdsAsync(parentsOfListed, ct);

        return items.Where(w =>
            listed.Any(d => d.Id == w.Id)
            || (w.StartDate == null && w.EndDate == null && ancestorsOfListed.Contains(w.Id)));
    }

    /// <summary>A missing start or end, on the item or the window, is open on that side.</summary>
    private static IQueryable<WorkItem> DatedInWindow(IQueryable<WorkItem> items, DateOnly? from, DateOnly? to) =>
        items.Where(w =>
            (w.StartDate != null || w.EndDate != null)
            && (to == null || w.StartDate == null || w.StartDate <= to)
            && (from == null || w.EndDate == null || w.EndDate >= from));

    private static IQueryable<WorkItem> InBacklog(IQueryable<WorkItem> items, bool backlogOnly) =>
        backlogOnly
            ? items.Where(w => w.StartDate == null && w.EndDate == null && w.Status!.Category != StatusCategory.Done)
            : items;

    private IQueryable<WorkItem> WithUnresolvedWhen(IQueryable<WorkItem> items, WorkItemQuery query)
    {
        if (!query.UnresolvedWhenOnly)
        {
            return query.IncludeEditedWhen
                ? throw new DomainValidationException("Include edited only applies with unresolved When only.")
                : items;
        }

        var blocked = _db.WorkItemLinks.BlockedIds();
        if (!query.IncludeEditedWhen)
        {
            return items.Where(WorkItem.WhenResolutionNeedsBlockersRule).Where(w => !blocked.Contains(w.Id));
        }

        // As id sets so the two rules can be OR-ed; each follows the outer query's Trash filter.
        var unresolved = _db.WorkItems.Where(WorkItem.WhenResolutionNeedsBlockersRule).Where(w => !blocked.Contains(w.Id)).Select(w => w.Id);
        var edited = _db.WhenEditedSinceResolvedIds();
        return items.Where(w => unresolved.Contains(w.Id) || edited.Contains(w.Id));
    }

    private static IQueryable<WorkItem> InSpaces(IQueryable<WorkItem> items, IReadOnlyList<Guid>? spaceIds) =>
        spaceIds is { Count: > 0 } ? items.Where(w => spaceIds.Contains(w.SpaceId)) : items;

    private IQueryable<WorkItem> MatchingText(IQueryable<WorkItem> items, string? text)
    {
        if (WorkItemNumberText.TryParse(text, out var typedNumber))
        {
            return Numbered(items, typedNumber);
        }

        return string.IsNullOrWhiteSpace(text) ? items : _search.MatchingText(items, text);
    }

    private static IQueryable<WorkItem> Numbered(IQueryable<WorkItem> items, int? number) =>
        number is { } n ? items.Where(w => w.Number == n) : items;

    private static IQueryable<WorkItem> InStatusCategories(IQueryable<WorkItem> items, IReadOnlyList<StatusCategory>? categories) =>
        categories is { Count: > 0 } ? items.Where(w => categories.Contains(w.Status!.Category)) : items;

    private static IQueryable<WorkItem> AssignedTo(IQueryable<WorkItem> items, Guid? assigneeId) =>
        assigneeId is { } id ? items.Where(w => w.AssignedToUserId == id) : items;

    /// <summary>
    /// Lower-cased because tags are stored as typed but count as one tag whatever their case,
    /// the way <see cref="WorkItem.SetTags"/> de-duplicates them.
    /// </summary>
    private IQueryable<WorkItem> Tagged(IQueryable<WorkItem> items, IReadOnlyList<string>? tags, TagMatch match)
    {
        var wanted = (tags ?? []).Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().ToList();
        return wanted.Count == 0 ? items : _search.Tagged(items, wanted, match);
    }

    private static IQueryable<WorkItem> OfTypes(IQueryable<WorkItem> items, IReadOnlyList<Guid>? typeIds) =>
        typeIds is { Count: > 0 } ? items.Where(w => w.TypeId != null && typeIds.Contains(w.TypeId.Value)) : items;

    private static IQueryable<WorkItem> UpdatedSince(IQueryable<WorkItem> items, DateTimeOffset? since)
    {
        if (since is null)
        {
            return items;
        }

        // Npgsql only accepts a UTC DateTimeOffset, and agents send times in their own offset.
        var utc = since.Value.ToUniversalTime();
        return items.Where(w => w.UpdatedAtUtc >= utc);
    }
}
