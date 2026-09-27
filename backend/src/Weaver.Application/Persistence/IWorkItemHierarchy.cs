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

    /// <summary>Every descendant of <paramref name="rootId"/>, at any depth.</summary>
    Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid rootId, CancellationToken ct = default);
}
