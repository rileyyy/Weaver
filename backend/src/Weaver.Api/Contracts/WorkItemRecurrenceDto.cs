using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// A work item's repetition, with enough of the work item to list it on its own.
/// <see cref="DaysOfWeek"/> is Monday-first and empty for month-based frequencies.
/// </summary>
public record WorkItemRecurrenceDto(
    Guid WorkItemId,
    int WorkItemNumber,
    string WorkItemTitle,
    Guid? ParentId,
    string? ParentTitle,
    RecurrenceFrequency Frequency,
    IReadOnlyList<DayOfWeek> DaysOfWeek,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly? NextOccurrence)
{
    public static WorkItemRecurrenceDto FromModel(RecurringWorkItem model) => new(
        model.WorkItemId,
        model.WorkItemNumber,
        model.WorkItemTitle,
        model.ParentId,
        model.ParentTitle,
        model.Schedule.Frequency,
        model.Schedule.DaysOfWeek,
        model.Schedule.StartDate,
        model.Schedule.EndDate,
        model.NextOccurrence);
}

public record SetWorkItemRecurrenceRequest(
    RecurrenceFrequency Frequency,
    IReadOnlyList<DayOfWeek>? DaysOfWeek,
    DateOnly StartDate,
    DateOnly? EndDate);
