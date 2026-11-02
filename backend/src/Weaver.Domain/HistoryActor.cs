namespace Weaver.Domain;

/// <summary>
/// Who a history entry is attributed to: the account, the API key it signed in with (agents
/// only) and the actor label the caller sent, if any. <see cref="System"/> has none of them:
/// it's Weaver itself, e.g. generating the occurrences of a repeating item.
/// </summary>
public sealed record HistoryActor
{
    public const int ActorLabelMaxLength = 100;

    private HistoryActor(Guid? accountId, Guid? apiKeyId, string? actorLabel)
    {
        AccountId = accountId;
        ApiKeyId = apiKeyId;
        ActorLabel = actorLabel;
    }

    public static HistoryActor System { get; } = new(null, null, null);

    public Guid? AccountId { get; }

    public Guid? ApiKeyId { get; }

    /// <summary>A free-text name for the part of an agent that made the change, e.g. "sub-agent: test-writer".</summary>
    public string? ActorLabel { get; }

    /// <summary>The label is trimmed, and a blank one counts as none.</summary>
    public static HistoryActor Create(Guid? accountId, Guid? apiKeyId, string? actorLabel)
    {
        var label = string.IsNullOrWhiteSpace(actorLabel) ? null : actorLabel.Trim();
        if (label is not null)
        {
            TextValidation.RequireMaxLength(label, "The actor label", ActorLabelMaxLength);
        }

        return new HistoryActor(accountId, apiKeyId, label);
    }
}
