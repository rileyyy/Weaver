namespace Weaver.Application.Services;

/// <summary>
/// Which work items are blocked: some live item has a blocks link to them. Readers use it to
/// resolve <see cref="Weaver.Domain.WorkItem.IsWhenResolved"/>, one query per batch of items.
/// </summary>
public interface IWorkItemBlockers
{
    /// <summary>The ids in <paramref name="workItemIds"/> that a live item blocks.</summary>
    Task<IReadOnlySet<Guid>> BlockedAmongAsync(IReadOnlyCollection<Guid> workItemIds, CancellationToken ct = default);
}
