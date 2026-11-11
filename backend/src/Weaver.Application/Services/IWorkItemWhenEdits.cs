namespace Weaver.Application.Services;

/// <summary>Which items have a When edited since it was resolved (ticket 49), one query per batch of items.</summary>
public interface IWorkItemWhenEdits
{
    /// <summary>Those of <paramref name="workItemIds"/> whose When a human edited after their dates were last set.</summary>
    Task<IReadOnlySet<Guid>> EditedSinceResolvedAsync(IReadOnlyCollection<Guid> workItemIds, CancellationToken ct = default);
}
