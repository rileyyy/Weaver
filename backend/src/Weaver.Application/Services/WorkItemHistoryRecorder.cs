using System.Text.Json;
using System.Text.Json.Serialization;
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
    /// <summary>The shape the values have in the API: camelCase, enums by name.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IWeaverDbContext _db;
    private readonly ICallerContext _caller;

    public WorkItemHistoryRecorder(IWeaverDbContext db, ICallerContext caller)
    {
        _db = db;
        _caller = caller;
    }

    /// <summary>The current caller, read when an entry is recorded rather than up front.</summary>
    public HistoryActor CurrentActor
    {
        get
        {
            var caller = _caller.Current;
            return HistoryActor.Create(caller.AccountId, caller.ApiKeyId, caller.ActorLabel);
        }
    }

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

    public void Record(Guid workItemId, string field, object? oldValue, object? newValue) =>
        Add(workItemId, CurrentActor, field, oldValue, newValue);

    public void Record(Guid workItemId, HistoryActor actor, string field, object? oldValue, object? newValue) =>
        Add(workItemId, actor, field, oldValue, newValue);

    private void Add(Guid workItemId, HistoryActor actor, string field, object? oldValue, object? newValue) =>
        _db.WorkItemHistory.Add(WorkItemHistoryEntry.Record(workItemId, actor, field, ToJson(oldValue), ToJson(newValue)));

    private static string? ToJson(object? value) => value is null ? null : JsonSerializer.Serialize(value, Json);
}
