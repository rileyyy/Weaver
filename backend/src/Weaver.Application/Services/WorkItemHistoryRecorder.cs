using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Adds history entries to the context without saving, so they're saved in the same
/// transaction as the change they describe: a write that fails records nothing. Every service
/// that changes a work item records through this (ADR-0005).
/// </summary>
internal sealed class WorkItemHistoryRecorder
{
    private readonly IWeaverDbContext _db;
    private readonly ICallerContext _caller;

    public WorkItemHistoryRecorder(IWeaverDbContext db, ICallerContext caller)
    {
        _db = db;
        _caller = caller;
    }

    /// <summary>The current caller, read when an entry is recorded rather than up front.</summary>
    public HistoryActor CurrentActor => _caller.Current.HistoryActor;

    /// <summary>
    /// Rank is left out: it's a card's position in its board cell, which every drag rewrites,
    /// not something a reader of the history means by a change.
    /// </summary>
    public void RecordChanges(Guid workItemId, IEnumerable<FieldChange> changes)
    {
        var actor = (HistoryActor?)null;
        foreach (var change in changes.Where(c => c.Field != WorkItemField.Rank))
        {
            actor ??= CurrentActor;
            Add(workItemId, actor, change.Field, change.OldValue, change.NewValue);
        }
    }

    /// <summary>
    /// A new item records its creation (with its title) and the Space it was created in, so
    /// its history says where it started even after a move between Spaces (ticket 16).
    /// </summary>
    public void RecordCreation(WorkItem item, HistoryActor actor)
    {
        Add(item.Id, actor, WorkItemHistoryField.Created, null, item.Title);
        Add(item.Id, actor, WorkItemField.SpaceId, null, item.SpaceId);
    }

    public void RecordCreation(WorkItem item) => RecordCreation(item, CurrentActor);

    public void Record(Guid workItemId, string field, object? oldValue, object? newValue) =>
        Add(workItemId, CurrentActor, field, oldValue, newValue);

    public void Record(Guid workItemId, HistoryActor actor, string field, object? oldValue, object? newValue) =>
        Add(workItemId, actor, field, oldValue, newValue);

    private void Add(Guid workItemId, HistoryActor actor, string field, object? oldValue, object? newValue) =>
        _db.WorkItemHistory.Add(WorkItemHistoryEntry.Record(
            workItemId, actor, field, HistoryJson.Serialize(oldValue), HistoryJson.Serialize(newValue)));
}
