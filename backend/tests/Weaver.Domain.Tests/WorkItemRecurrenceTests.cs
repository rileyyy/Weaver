namespace Weaver.Domain.Tests;

[TestFixture]
public class WorkItemRecurrenceTests
{
    [Test]
    public void ApplySchedule_CopiesTheScheduleAndClearsGeneratedThrough()
    {
        var recurrence = WorkItemRecurrence.Create(
            Guid.NewGuid(),
            RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, [DayOfWeek.Monday], new DateOnly(2026, 1, 5), null));
        recurrence.MarkGeneratedThrough(new DateOnly(2026, 2, 1));
        var schedule = RecurrenceSchedule.Create(
            RecurrenceFrequency.Monthly, [], new DateOnly(2026, 3, 1), new DateOnly(2026, 12, 1));

        recurrence.ApplySchedule(schedule);

        Assert.That(recurrence.Schedule, Is.EqualTo(schedule));
        Assert.That(recurrence.GeneratedThrough, Is.Null);
    }
}
