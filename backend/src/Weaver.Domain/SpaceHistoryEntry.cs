namespace Weaver.Domain;

/// <summary>
/// One recorded change to a Space: its creation, a rename or a settings change, who made it and when. Kept apart
/// from <see cref="WorkItemHistoryEntry"/> because a Space isn't a work item, and append-only
/// like it. Values are JSON, in the shape the field has in the API.
/// </summary>
public class SpaceHistoryEntry : IHasCreatedAt
{
    /// <summary>For EF Core.</summary>
    private SpaceHistoryEntry()
    {
        Field = string.Empty;
    }

    /// <summary>Database-generated and increasing, so it orders entries saved in the same instant.</summary>
    public long Id { get; private set; }

    public Guid SpaceId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid? AccountId { get; private set; }

    public Guid? ApiKeyId { get; private set; }

    public string? ActorLabel { get; private set; }

    /// <summary>A <see cref="SpaceHistoryField"/> name.</summary>
    public string Field { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public static SpaceHistoryEntry Record(
        Guid spaceId,
        HistoryActor actor,
        string field,
        string? oldValueJson,
        string? newValueJson) => new()
        {
            SpaceId = spaceId,
            AccountId = actor.AccountId,
            ApiKeyId = actor.ApiKeyId,
            ActorLabel = actor.ActorLabel,
            Field = field,
            OldValue = oldValueJson,
            NewValue = newValueJson,
        };
}
