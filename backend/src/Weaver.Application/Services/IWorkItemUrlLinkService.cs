namespace Weaver.Application.Services;

/// <summary>Links from a work item to addresses outside Weaver.</summary>
public interface IWorkItemUrlLinkService
{
    /// <summary>The item's URL links, oldest first.</summary>
    Task<IReadOnlyList<WorkItemUrlLinkView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default);

    /// <summary>
    /// Throws <see cref="Weaver.Domain.Exceptions.DomainValidationException"/> unless
    /// <paramref name="url"/> is an absolute http or https address; a blank label is no label.
    /// </summary>
    Task<WorkItemUrlLinkView> CreateAsync(Guid workItemId, string url, string? label, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
