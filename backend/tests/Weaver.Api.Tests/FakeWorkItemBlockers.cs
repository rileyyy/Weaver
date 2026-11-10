using Weaver.Application.Services;

namespace Weaver.Api.Tests;

/// <summary>Reports the given blockers for each item, and remembers which ids each call asked about.</summary>
public sealed class FakeWorkItemBlockers(IReadOnlyDictionary<Guid, IReadOnlyList<WorkItemBlocker>>? blockers = null) : IWorkItemBlockers
{
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<WorkItemBlocker>> _blockers =
        blockers ?? new Dictionary<Guid, IReadOnlyList<WorkItemBlocker>>();

    public List<IReadOnlyCollection<Guid>> Requests { get; } = [];

    /// <summary>Each of <paramref name="blocked"/> has one blocker, which doesn't conflict.</summary>
    public static FakeWorkItemBlockers Blocking(params Guid[] blocked) =>
        new(blocked.ToDictionary(
            id => id,
            _ => (IReadOnlyList<WorkItemBlocker>)[new WorkItemBlocker(Guid.NewGuid(), 1, "Blocker", Guid.NewGuid(), null, false)]));

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<WorkItemBlocker>>> BlockersOfAsync(
        IReadOnlyCollection<Guid> workItemIds,
        CancellationToken ct = default)
    {
        Requests.Add(workItemIds);
        IReadOnlyDictionary<Guid, IReadOnlyList<WorkItemBlocker>> found = workItemIds
            .Where(_blockers.ContainsKey)
            .ToDictionary(id => id, id => _blockers[id]);
        return Task.FromResult(found);
    }
}
