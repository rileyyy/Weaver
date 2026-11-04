using Weaver.Application.Services;

namespace Weaver.Api.Tests;

/// <summary>Reports the given items as blocked, and remembers which ids each call asked about.</summary>
public sealed class FakeWorkItemBlockers(params Guid[] blocked) : IWorkItemBlockers
{
    private readonly HashSet<Guid> _blocked = [.. blocked];

    public List<IReadOnlyCollection<Guid>> Requests { get; } = [];

    public Task<IReadOnlySet<Guid>> BlockedAmongAsync(IReadOnlyCollection<Guid> workItemIds, CancellationToken ct = default)
    {
        Requests.Add(workItemIds);
        return Task.FromResult<IReadOnlySet<Guid>>(workItemIds.Where(_blocked.Contains).ToHashSet());
    }
}
