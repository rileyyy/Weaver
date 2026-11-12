using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>The history entries one run made, or the ones it didn't, named by its <see cref="RunKey"/>.</summary>
internal static class RunHistoryQueries
{
    public static IQueryable<WorkItemHistoryEntry> OfRun(this IQueryable<WorkItemHistoryEntry> entries, RunKey run) =>
        entries.Where(e => e.RunId == run.RunId && e.AccountId == run.AccountId && e.ApiKeyId == run.ApiKeyId);

    public static IQueryable<WorkItemHistoryEntry> NotOfRun(this IQueryable<WorkItemHistoryEntry> entries, RunKey run) =>
        entries.Where(e => e.RunId != run.RunId || e.AccountId != run.AccountId || e.ApiKeyId != run.ApiKeyId);
}
