using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemRecurrenceTools
{
    private readonly IWorkItemRecurrenceService _recurrences;

    public WorkItemRecurrenceTools(IWorkItemRecurrenceService recurrences)
    {
        _recurrences = recurrences;
    }

    [McpServerTool(Name = "list_repeating_work_items", ReadOnly = true)]
    [Description("Lists every repeating work item with its schedule and next occurrence date.")]
    public async Task<IReadOnlyList<WorkItemRecurrenceDto>> ListRepeatingWorkItems(CancellationToken ct)
    {
        var recurrences = await _recurrences.ListAsync(ct);
        return recurrences.Select(WorkItemRecurrenceDto.FromModel).ToList();
    }

    [McpServerTool(Name = "get_work_item_recurrence", ReadOnly = true)]
    [Description("Gets a work item's repeat schedule, or null if it doesn't repeat.")]
    public Task<WorkItemRecurrenceDto?> GetWorkItemRecurrence(
        [Description("The work item's id.")] Guid workItemId,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var recurrence = await _recurrences.GetAsync(workItemId, ct);
            return recurrence is null ? null : WorkItemRecurrenceDto.FromModel(recurrence);
        });

    [McpServerTool(Name = "set_work_item_recurrence", Destructive = false)]
    [Description("Makes a work item repeat, or replaces its schedule. A copy of the item and its sub-items " +
        "is created under the same parent a week before each occurrence date. Weekly and BiWeekly repeat on " +
        "the given days of the week (at least one required); Monthly, Quarterly and Yearly repeat on the start " +
        "date's day of the month and ignore daysOfWeek.")]
    public Task<WorkItemRecurrenceDto> SetWorkItemRecurrence(
        [Description("The work item's id.")] Guid workItemId,
        [Description("Weekly, BiWeekly, Monthly, Quarterly or Yearly.")] RecurrenceFrequency frequency,
        [Description("Days of the week (e.g. Monday) for Weekly/BiWeekly.")] IReadOnlyList<DayOfWeek>? daysOfWeek,
        [Description("First date the schedule can fall on (yyyy-MM-dd).")] DateOnly startDate,
        [Description("Last date the schedule can fall on (yyyy-MM-dd). Omit to repeat indefinitely.")] DateOnly? endDate,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var recurrence = await _recurrences.SetAsync(workItemId, frequency, daysOfWeek ?? [], startDate, endDate, ct);
            return WorkItemRecurrenceDto.FromModel(recurrence);
        });

    [McpServerTool(Name = "remove_work_item_recurrence", Destructive = true)]
    [Description("Stops a work item repeating. Items it already created are kept.")]
    public Task RemoveWorkItemRecurrence(
        [Description("The work item's id.")] Guid workItemId,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(() => _recurrences.RemoveAsync(workItemId, ct));
}
