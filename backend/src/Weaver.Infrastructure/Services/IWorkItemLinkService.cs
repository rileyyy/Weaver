using Weaver.Domain;

namespace Weaver.Infrastructure.Services;

public interface IWorkItemLinkService
{
    /// <summary>
    /// Every link touching <paramref name="workItemId"/>, on either side —
    /// a link is symmetric, so this returns it regardless of whether
    /// <paramref name="workItemId"/> was the one that created it.
    /// </summary>
    Task<IReadOnlyList<WorkItemLink>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default);

    /// <summary>
    /// Throws <see cref="Weaver.Domain.Exceptions.SelfWorkItemLinkException"/> if the two ids
    /// are the same, or <see cref="Weaver.Domain.Exceptions.DuplicateWorkItemLinkException"/>
    /// if they're already linked (in either direction).
    /// </summary>
    Task<WorkItemLink> CreateAsync(Guid workItemId, Guid targetWorkItemId, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
