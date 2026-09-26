using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// A repetition as read: its schedule, enough of its work item (and that item's parent, so a
/// list outside any lane can say where copies land) to show it on its own, and the next date
/// it falls on from today (null once it has ended).
/// </summary>
public record RecurringWorkItem(
    Guid WorkItemId,
    int WorkItemNumber,
    string WorkItemTitle,
    Guid? ParentId,
    string? ParentTitle,
    RecurrenceSchedule Schedule,
    DateOnly? NextOccurrence);
