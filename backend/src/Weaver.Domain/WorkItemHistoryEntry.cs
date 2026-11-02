namespace Weaver.Domain;

/// <summary>
/// One recorded change to a work item: who made it, when, which field, and the value before
/// and after. Entries are only ever added: nothing edits or deletes them, and they outlive
/// the item, so a delete stays visible.
/// </summary>
/// <remarks>
/// <see cref="Field"/> is a <see cref="WorkItemField"/> name for a field change, or a
/// <see cref="WorkItemHistoryField"/> name for everything else. Values are stored as JSON, in
/// the shape the field has in the API, so a list or a date survives the round trip.
/// </remarks>
public class WorkItemHistoryEntry : IHasCreatedAt
{
    /// <summary>For EF Core.</summary>
    private WorkItemHistoryEntry()
    {
        Field = string.Empty;
    }

    /// <summary>Database-generated and increasing, so it orders entries saved in the same instant.</summary>
    public long Id { get; private set; }

    /// <summary>Deliberately not a foreign key: the entries must outlive a deleted item.</summary>
    public Guid WorkItemId { get; private set; }

    /// <summary>When the change was saved.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Null when Weaver itself made the change (<see cref="HistoryActor.System"/>).</summary>
    public Guid? AccountId { get; private set; }

    public Guid? ApiKeyId { get; private set; }

    public string? ActorLabel { get; private set; }

    public string Field { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public User? Account { get; private set; }

    public ApiKey? ApiKey { get; private set; }

    public static WorkItemHistoryEntry Record(
        Guid workItemId,
        HistoryActor actor,
        string field,
        string? oldValueJson,
        string? newValueJson) => new()
        {
            WorkItemId = workItemId,
            AccountId = actor.AccountId,
            ApiKeyId = actor.ApiKeyId,
            ActorLabel = actor.ActorLabel,
            Field = field,
            OldValue = oldValueJson,
            NewValue = newValueJson,
        };
}
