using Weaver.Domain;

namespace Weaver.Application.Services;

internal static class ViewQueries
{
    /// <summary>The work-item query a View stands for, over <paramref name="window"/>.</summary>
    public static WorkItemQuery ToQuery(this View view, ViewWindow window) => new(
        SpaceIds: view.SpaceIds,
        Text: view.Text,
        Tags: view.Tags,
        TagMatch: view.TagMatch,
        From: window.From,
        To: window.To,
        ScheduledOrInProgressOnly: view.ScheduledOrInProgressOnly,
        ApplyDoneHiding: view.ApplyDoneHiding,
        Cursor: window.Cursor,
        Limit: window.Limit);
}
