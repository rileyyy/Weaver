using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

/// <summary>Answers every lookup with the given names, and remembers which items each call asked about.</summary>
public sealed class FakeWorkItemNameLookup(WorkItemNames? names = null) : IWorkItemNameLookup
{
    private readonly WorkItemNames _names = names ?? WorkItemNames.None;

    public List<IReadOnlyCollection<WorkItem>> Requests { get; } = [];

    public Task<WorkItemNames> NamesForAsync(IReadOnlyCollection<WorkItem> items, CancellationToken ct = default)
    {
        Requests.Add(items);
        return Task.FromResult(_names);
    }
}
