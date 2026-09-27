using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// A repetition as read: its schedule, enough of its work item to list it on its own, and the
/// next date it falls on from today (null once it has ended).
/// </summary>
public record RecurringWorkItem(
    Guid WorkItemId,
    int WorkItemNumber,
    string WorkItemTitle,
    Guid? ParentId,
    RecurrenceSchedule Schedule,
    DateOnly? NextOccurrence);
