namespace Weaver.Domain;

/// <summary>
/// Who a history entry is attributed to: the account, the API key it signed in with (agents
/// only), and the actor label and run id the caller sent, if any. <see cref="System"/> has none
/// of them: it's Weaver itself, e.g. generating the occurrences of a repeating item.
/// </summary>
public sealed record HistoryActor
{
    public const int ActorLabelMaxLength = 100;

    /// <summary>Room for a GUID or a readable name such as "when-resolution 2026-11-11".</summary>
    public const int RunIdMaxLength = 100;

    private HistoryActor(Guid? accountId, Guid? apiKeyId, string? actorLabel, string? runId)
    {
        AccountId = accountId;
        ApiKeyId = apiKeyId;
        ActorLabel = actorLabel;
        RunId = runId;
    }

    public static HistoryActor System { get; } = new(null, null, null, null);

    public Guid? AccountId { get; }

    public Guid? ApiKeyId { get; }

    /// <summary>A free-text name for the part of an agent that made the change, e.g. "sub-agent: test-writer".</summary>
    public string? ActorLabel { get; }

    /// <summary>
    /// Free text an agent sends with every write of one run, so the run's changes can be
    /// reported together (ADR-0010). Weaver doesn't generate or check it beyond its length.
    /// </summary>
    public string? RunId { get; }

    /// <summary>The label and run id are trimmed, and a blank one counts as none.</summary>
    public static HistoryActor Create(Guid? accountId, Guid? apiKeyId, string? actorLabel, string? runId = null) =>
        new(
            accountId,
            apiKeyId,
            Optional(actorLabel, "The actor label", ActorLabelMaxLength),
            Optional(runId, "The run id", RunIdMaxLength));

    private static string? Optional(string? text, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        TextValidation.RequireMaxLength(trimmed, name, maxLength);
        return trimmed;
    }
}
