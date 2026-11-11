using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Saved, shared Views (ADR-0006). Anyone who may write can save, change or delete one, and each
/// user picks their own default. Overview (<see cref="View.OverviewId"/>) is built in: it's
/// listed, read and run like the others, but can't be changed or deleted.
/// </summary>
public interface IViewService
{
    /// <summary>Overview first, then every saved View by name.</summary>
    Task<IReadOnlyList<View>> ListAsync(CancellationToken ct = default);

    Task<View> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Rejects a name another View has, ignoring case ("Overview" included), and a Space that
    /// doesn't exist.
    /// </summary>
    Task<View> CreateAsync(ViewDefinition definition, CancellationToken ct = default);

    /// <summary>
    /// Replaces the View's whole definition, its name included, on the same terms as
    /// <see cref="CreateAsync"/>.
    /// </summary>
    Task<View> UpdateAsync(Guid id, ViewDefinition definition, CancellationToken ct = default);

    /// <summary>Anyone who had it as their default falls back to Overview.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>The caller's default View, or Overview when they haven't picked one.</summary>
    Task<View> GetDefaultAsync(CancellationToken ct = default);

    /// <summary>Makes <paramref name="viewId"/> the caller's default; Overview's id clears the choice.</summary>
    Task<View> SetDefaultAsync(Guid viewId, CancellationToken ct = default);

    /// <summary>One page of the View's items, in brief, over <paramref name="window"/>.</summary>
    Task<WorkItemQueryPage<WorkItemSummary>> RunAsync(Guid id, ViewWindow window, CancellationToken ct = default);
}
