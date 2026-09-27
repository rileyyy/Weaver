namespace Weaver.Domain;

/// <summary>
/// Makes a work item repeat: on each date of its schedule, a copy of the item (with its
/// sub-items) is created under the item's current parent, <see cref="LeadDays"/> ahead of
/// time. The item itself is the template and is never changed by generation. Keyed by the
/// work item's id — an item has at most one repetition.
/// </summary>
public class WorkItemRecurrence : IHasUpdatedAt
{
    public const int LeadDays = 7;

    /// <summary>For EF Core.</summary>
    private WorkItemRecurrence()
    {
    }

    public Guid WorkItemId { get; private set; }

    public RecurrenceFrequency Frequency { get; private set; }

    public RecurrenceDays Days { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    /// <summary>
    /// The last date generation has already considered. Occurrences on or before it are never
    /// created again, so deleting a generated item doesn't bring it back. Cleared whenever the
    /// schedule changes, so the new schedule's upcoming occurrences get created.
    /// </summary>
    public DateOnly? GeneratedThrough { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public WorkItem? WorkItem { get; private set; }

    public RecurrenceSchedule Schedule => RecurrenceSchedule.FromStored(Frequency, Days, StartDate, EndDate);

    public static WorkItemRecurrence Create(Guid workItemId, RecurrenceSchedule schedule)
    {
        var recurrence = new WorkItemRecurrence { WorkItemId = workItemId };
        recurrence.ApplySchedule(schedule);
        return recurrence;
    }

    /// <summary>Records that every occurrence up to <paramref name="date"/> has been considered.</summary>
    public void MarkGeneratedThrough(DateOnly date)
    {
        GeneratedThrough = date;
    }

    public void ApplySchedule(RecurrenceSchedule schedule)
    {
        Frequency = schedule.Frequency;
        Days = schedule.Days;
        StartDate = schedule.StartDate;
        EndDate = schedule.EndDate;
        GeneratedThrough = null;
    }
}
