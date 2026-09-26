using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// A repetition with its work item loaded (<see cref="WorkItemRecurrence.WorkItem"/>) and
/// the next date it falls on from today, or null once it has ended.
/// </summary>
public record RecurringWorkItem(WorkItemRecurrence Recurrence, DateOnly? NextOccurrence);
