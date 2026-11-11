using Weaver.Application.Services;

namespace Weaver.Api.Tests;

/// <summary>Reports the given items as edited since resolved, and remembers which ids each call asked about.</summary>
public sealed class FakeWorkItemWhenEdits(params Guid[] edited) : IWorkItemWhenEdits
{
    private readonly HashSet<Guid> _edited = [.. edited];

    public List<IReadOnlyCollection<Guid>> Requests { get; } = [];

    public Task<IReadOnlySet<Guid>> EditedSinceResolvedAsync(IReadOnlyCollection<Guid> workItemIds, CancellationToken ct = default)
    {
        Requests.Add(workItemIds);
        IReadOnlySet<Guid> found = workItemIds.Where(_edited.Contains).ToHashSet();
        return Task.FromResult(found);
    }
}
