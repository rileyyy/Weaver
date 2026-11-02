using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemRecurrenceService : IWorkItemRecurrenceService
{
    private readonly IWeaverDbContext _db;
    private readonly IServerCalendar _calendar;
    private readonly ILogger<WorkItemRecurrenceService> _logger;
    private readonly WorkItemHistoryRecorder _history;

    public WorkItemRecurrenceService(
        IWeaverDbContext db,
        IServerCalendar calendar,
        ILogger<WorkItemRecurrenceService> logger,
        ICallerContext caller)
    {
        _db = db;
        _calendar = calendar;
        _logger = logger;
        _history = new WorkItemHistoryRecorder(db, caller);
    }

    public async Task<RecurringWorkItem?> GetAsync(Guid workItemId, CancellationToken ct = default)
    {
        var item = await FindWorkItemAsync(workItemId, ct);
        var recurrence = await _db.WorkItemRecurrences.FindAsync([workItemId], ct);
        if (recurrence is null)
        {
            return null;
        }

        return Describe(recurrence, item, await ParentTitleAsync(item, ct), await _calendar.TodayAsync(ct));
    }

    public async Task<IReadOnlyList<RecurringWorkItem>> ListAsync(CancellationToken ct = default)
    {
        var recurrences = await _db.WorkItemRecurrences
            .AsNoTracking()
            .Include(r => r.WorkItem)
            .ThenInclude(w => w!.Parent)
            .OrderBy(r => r.WorkItem!.Number)
            .ToListAsync(ct);

        var today = await _calendar.TodayAsync(ct);
        return recurrences.Select(r => Describe(r, r.WorkItem!, r.WorkItem!.Parent?.Title, today)).ToList();
    }

    public async Task<RecurringWorkItem> SetAsync(
        Guid workItemId,
        RecurrenceFrequency frequency,
        IReadOnlyCollection<DayOfWeek> daysOfWeek,
        DateOnly startDate,
        DateOnly? endDate,
        CancellationToken ct = default)
    {
        var schedule = RecurrenceSchedule.Create(frequency, daysOfWeek, startDate, endDate);
        var item = await FindWorkItemAsync(workItemId, ct);

        // Otherwise every occurrence would spawn its own series from the next window on,
        // multiplying the copies each time.
        if (item.RecurrenceSourceId is not null)
        {
            throw new DomainValidationException(
                "This item was created by a repeating item. Change the repeat settings on that item instead.");
        }

        var recurrence = await _db.WorkItemRecurrences.FindAsync([workItemId], ct);
        var previous = recurrence?.Schedule;
        if (recurrence is null)
        {
            recurrence = WorkItemRecurrence.Create(workItemId, schedule);
            _db.WorkItemRecurrences.Add(recurrence);
        }
        else
        {
            recurrence.ApplySchedule(schedule);
        }

        if (previous != schedule)
        {
            _history.Record(workItemId, WorkItemHistoryField.Recurrence, HistoryValue(previous), HistoryValue(schedule));
        }
        await _db.SaveChangesAsync(ct);

        var today = await _calendar.TodayAsync(ct);
        await GenerateForAsync(recurrence, item, NewOccurrenceFactory(_history.CurrentActor), today, ct);
        return Describe(recurrence, item, await ParentTitleAsync(item, ct), today);
    }

    public async Task RemoveAsync(Guid workItemId, CancellationToken ct = default)
    {
        await FindWorkItemAsync(workItemId, ct);
        var recurrence = await _db.WorkItemRecurrences.FindAsync([workItemId], ct);
        if (recurrence is null)
        {
            return;
        }

        _db.WorkItemRecurrences.Remove(recurrence);
        _history.Record(workItemId, WorkItemHistoryField.Recurrence, HistoryValue(recurrence.Schedule), null);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GenerateDueAsync(CancellationToken ct = default)
    {
        var today = await _calendar.TodayAsync(ct);
        var horizon = today.AddDays(WorkItemRecurrence.LeadDays);
        var dueIds = await _db.WorkItemRecurrences
            .Where(r => r.GeneratedThrough == null ||
                (r.GeneratedThrough < horizon && (r.EndDate == null || r.EndDate > r.GeneratedThrough)))
            .Select(r => r.WorkItemId)
            .ToListAsync(ct);

        var created = 0;
        foreach (var id in dueIds)
        {
            try
            {
                var recurrence = await _db.WorkItemRecurrences.Include(r => r.WorkItem).FirstAsync(r => r.WorkItemId == id, ct);
                created += await GenerateForAsync(
                    recurrence, recurrence.WorkItem!, NewOccurrenceFactory(HistoryActor.System), today, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Generating occurrences for repeating work item {WorkItemId} failed", id);
                _db.ChangeTracker.Clear();
            }
        }

        return created;
    }

    private async Task<int> GenerateForAsync(
        WorkItemRecurrence recurrence,
        WorkItem template,
        OccurrenceFactory factory,
        DateOnly today,
        CancellationToken ct)
    {
        var horizon = today.AddDays(WorkItemRecurrence.LeadDays);
        var from = recurrence.GeneratedThrough?.AddDays(1) ?? today;

        var existing = await _db.WorkItems
            .Where(w => w.RecurrenceSourceId == template.Id && w.RecurrenceDate >= from && w.RecurrenceDate <= horizon)
            .Select(w => w.RecurrenceDate!.Value)
            .ToListAsync(ct);

        // The template scheduled on an occurrence date stands in for that occurrence;
        // otherwise setting up "every Monday" on a Monday task would duplicate it at once.
        var dates = recurrence.Schedule.OccurrencesBetween(from, horizon)
            .Where(d => d != template.StartDate && !existing.Contains(d))
            .ToList();

        foreach (var date in dates)
        {
            await factory.AddOccurrenceAsync(template, date, ct);
            // One save per occurrence: EF orders a batch's inserts by primary key, and
            // random GUIDs would hand out display numbers (#N) out of date order.
            await _db.SaveChangesAsync(ct);
        }

        recurrence.MarkGeneratedThrough(horizon);
        await _db.SaveChangesAsync(ct);
        return dates.Count;
    }

    /// <summary>Background generation has no caller: its occurrences are created by Weaver itself.</summary>
    private OccurrenceFactory NewOccurrenceFactory(HistoryActor actor) => new(_db, _history, actor);

    /// <summary>The schedule in the shape the API shows it.</summary>
    private static object? HistoryValue(RecurrenceSchedule? schedule) =>
        schedule is null
            ? null
            : new { schedule.Frequency, schedule.DaysOfWeek, schedule.StartDate, schedule.EndDate };

    private static RecurringWorkItem Describe(
        WorkItemRecurrence recurrence,
        WorkItem item,
        string? parentTitle,
        DateOnly today) => new(
        item.Id,
        item.Number,
        item.Title,
        item.ParentId,
        parentTitle,
        recurrence.Schedule,
        recurrence.Schedule.NextOccurrenceOnOrAfter(today));

    private async Task<string?> ParentTitleAsync(WorkItem item, CancellationToken ct) =>
        item.ParentId is null
            ? null
            : await _db.WorkItems.Where(w => w.Id == item.ParentId).Select(w => w.Title).FirstOrDefaultAsync(ct);

    private async Task<WorkItem> FindWorkItemAsync(Guid id, CancellationToken ct) =>
        await _db.WorkItems.FindAsync([id], ct) ?? throw new EntityNotFoundException(nameof(WorkItem), id);
}
