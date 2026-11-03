using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// What history still says about an item whose row is gone: its last title and the Space it
/// was last in. History is the only record of a deleted item (ADR-0005).
/// </summary>
internal sealed record DeletedItemTrace(Guid WorkItemId, string Title, Guid? SpaceId)
{
    private static readonly string[] Fields =
    [
        WorkItemHistoryField.Created, WorkItemField.Title, WorkItemHistoryField.Deleted, WorkItemField.SpaceId,
    ];

    /// <summary>An item whose history holds none of the entries a trace is built from.</summary>
    public static DeletedItemTrace Unknown(Guid workItemId) => new(workItemId, string.Empty, null);

    /// <summary>Everything else a summary holds died with the row.</summary>
    public WorkItemSummary ToSummary(string? spaceName) => new(
        WorkItemId,
        Number: null,
        Title,
        SpaceId,
        spaceName,
        StatusName: null,
        TypeName: null,
        StartDate: null,
        EndDate: null,
        AssigneeName: null,
        ChildCount: null);

    /// <summary>
    /// Reads all of each item's history, not just since T: the Space is often only in the
    /// entry that created it. An item created before history recorded its Space has none.
    /// </summary>
    public static async Task<Dictionary<Guid, DeletedItemTrace>> LoadAsync(
        IWeaverDbContext db,
        IReadOnlyCollection<Guid> workItemIds,
        CancellationToken ct)
    {
        var entries = await db.WorkItemHistory
            .AsNoTracking()
            .Where(e => workItemIds.Contains(e.WorkItemId) && Fields.Contains(e.Field))
            .OrderBy(e => e.Id)
            .Select(e => new { e.WorkItemId, e.Field, e.OldValue, e.NewValue })
            .ToListAsync(ct);

        return entries
            .GroupBy(e => e.WorkItemId)
            .ToDictionary(
                g => g.Key,
                g => g.Aggregate(
                    Unknown(g.Key),
                    (trace, e) => e.Field switch
                    {
                        WorkItemField.SpaceId => trace with { SpaceId = Read<Guid?>(e.NewValue) },
                        WorkItemHistoryField.Deleted => trace with { Title = Read<string>(e.OldValue) ?? trace.Title },
                        _ => trace with { Title = Read<string>(e.NewValue) ?? trace.Title },
                    }));
    }

    private static T? Read<T>(string? json) => json is null ? default : JsonSerializer.Deserialize<T>(json);
}
