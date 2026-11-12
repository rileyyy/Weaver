namespace Weaver.Application.Services;

/// <summary>Mentions of the calling account in comments (ticket 51).</summary>
public interface IMentionService
{
    /// <summary>
    /// The caller's pending mentions, oldest first. A mention is pending until the caller
    /// comments on the same item after it. Mentions in comments written by agent accounts are
    /// never pending, so agents can't set each other off in a loop.
    /// </summary>
    Task<IReadOnlyList<PendingMention>> ListPendingAsync(CancellationToken ct = default);
}
