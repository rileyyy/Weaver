using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IWorkItemLinkService
{
    /// <summary>
    /// Every link touching <paramref name="workItemId"/>, on either side, seen from
    /// <paramref name="workItemId"/>, oldest first.
    /// </summary>
    Task<IReadOnlyList<WorkItemLinkView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default);

    /// <summary>
    /// Links two items, possibly in different Spaces. <paramref name="type"/> is how
    /// <paramref name="workItemId"/> relates to <paramref name="targetWorkItemId"/>, and the result
    /// is seen from <paramref name="workItemId"/>. Two items have at most one link, of any type.
    /// Throws <see cref="Weaver.Domain.Exceptions.SelfWorkItemLinkException"/> if the two ids are
    /// the same, <see cref="Weaver.Domain.Exceptions.BlockingCycleException"/> for a blocks link
    /// against an existing one the other way, and otherwise
    /// <see cref="Weaver.Domain.Exceptions.DuplicateWorkItemLinkException"/> if they're already linked.
    /// </summary>
    Task<WorkItemLinkView> CreateAsync(
        Guid workItemId,
        Guid targetWorkItemId,
        WorkItemLinkRelation type,
        CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
