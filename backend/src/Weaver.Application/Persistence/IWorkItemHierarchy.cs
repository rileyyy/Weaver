namespace Weaver.Application.Persistence;

/// <summary>
/// Tree walks over the work item hierarchy. Kept apart from <see cref="IWeaverDbContext"/>
/// because the efficient form is a recursive SQL query, which only the database provider
/// can run.
/// </summary>
public interface IWorkItemHierarchy
{
    /// <summary>
    /// True if <paramref name="candidateId"/> is <paramref name="itemId"/> or one of its
    /// descendants, i.e. making it the item's parent would create a cycle. Throws
    /// <see cref="InvalidOperationException"/> if the tree above the candidate already contains
    /// a cycle, since that state is corruption, not an answer.
    /// </summary>
    Task<bool> IsSelfOrDescendantAsync(Guid itemId, Guid candidateId, CancellationToken ct = default);

    /// <summary>
    /// Every live descendant of <paramref name="rootId"/>, at any depth. A trashed item and
    /// everything under it are left out, unless <paramref name="includeTrashed"/>.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid rootId, bool includeTrashed = false, CancellationToken ct = default);

    /// <summary>
    /// <paramref name="itemIds"/> that exist, and every ancestor of each, at any depth, each id
    /// once, trashed or not. Ends even on a corrupt tree with a cycle.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetSelfAndAncestorIdsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct = default);
}
